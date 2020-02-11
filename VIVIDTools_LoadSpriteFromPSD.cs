using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using System.Linq;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.U2D;
using UnityEngine.EventSystems;


public class VIVIDTools_LoadSpriteFromPSD : EditorWindow
{
    private const string STR_ParseIt = "Parse JSON";

    [MenuItem("[VividARTs]/Tools/Import Sprite from PSD")]
    static void Init()
    {
        VIVIDTools_LoadSpriteFromPSD window = (VIVIDTools_LoadSpriteFromPSD)EditorWindow.GetWindow(typeof(VIVIDTools_LoadSpriteFromPSD));
    }
   
    [System.Serializable]
    public class GetTreeJson
    {
        public string Name;
        public string Type;
        public float Width;
        public float Height;
        public float[] Pos;
        public AnchorType Anchor;
        public AnchorType Pivot;
        public string Sprite;
        public bool HaveChild;
        public int ChildsCount;
        public List<GetTreeJson> Childrens = new List<GetTreeJson>();
    }
    [System.Serializable]
    public class SpriteLibDB
    {
        public List<SpriteIndex> Idx = new List<SpriteIndex>();
    }
    [System.Serializable]
    public class SpriteIndex
    {
        public string SpriteName;
        public string TextureName;
        public float[] TextureSize;
        public int TextureType;
        public string Path;
        public float[] Pos;
        public bool isFrame;
        public float[] Border;
        public int Pivot;
        public float[] CustomPivot;
    }


    [System.Serializable]
    public enum AnchorType
    {
        AllStretch=0,           TopLeft = 1,            TopCenter = 2,          TopRight = 3,
        MiddleLeft = 4,         MiddleCenter = 5,       MiddleRight = 6,        BottomLeft = 7,
        BottomCenter = 8,       BottomRight = 9,        TopStretch=10,          MiddleStretch=11,
        BottomStretch=12,       LeftStretch=13,         CenterStretch=14,       RightStretch=15
    }
    public Vector4[] AnchorValue = { 
        new Vector4(0f, 0f,     1f, 1f),        new Vector4(0f, 1f,     0f, 1f),        new Vector4(0.5f, 1f,   0.5f, 1f),
        new Vector4(1f, 1f,     1f, 1f),        new Vector4(0f, 0.5f,   0f, 0.5f),      new Vector4(0.5f, 0.5f, 0.5f, 0.5f),
        new Vector4(1f, 0.5f,   1f, 0.5f),      new Vector4(0f, 0f,     0f, 0f),        new Vector4(0.5f, 0f,   0.5f, 0f),
        new Vector4(1f, 0f,     1f, 0f),        new Vector4(0f, 1f,     1f, 1f),        new Vector4(0f, 0.5f,   1f, 0.5f),
        new Vector4(0f, 0f,     1f, 0f),        new Vector4(0f, 0f,     0f, 1f),        new Vector4(0.5f, 0.5f, 0f, 1f),
        new Vector4(1f, 0f,     1f, 1f)};

    public Vector2[] PivotValue = {
        new Vector2(0.5f, 0.5f),    new Vector2(0f, 1f),        new Vector2(0.5f, 1f),
        new Vector2(1f, 1f),        new Vector2(0f, 0.5f),      new Vector2(0.5f, 0.5f),
        new Vector2(1f, 0.5f),      new Vector2(0f, 0f),        new Vector2(0.5f, 0f),
        new Vector2(1f, 0f),        new Vector2(0.5f, 1f),      new Vector2(0.5f, 0.5f),
        new Vector2(0.5f, 0f),      new Vector2(0f, 0.5f),      new Vector2(0.5f, 0.5f),
        new Vector2(1f, 0.5f)};

    public String targetFolder;
    public TextAsset JsonData;    
    public Transform rootObject;
    public GameObject root;
    public TextAsset JsonSpriteLib;
    public SpriteIndex[] SIdx;

    public SpriteAtlas SpriteAtlasLoaded;
    public Sprite[] SpritesArray;

    public Sprite isLoadedSprite;
    private MonoScript targetMonoScript;
    private Vector2 scrollPos;

    private void OnGUI()
    {
        GUILayout.Label("Load UGUI Sprite File", EditorStyles.boldLabel);
        GUILayout.Label("Load UGUI Sprite Sheet (JSON)\n");
        JsonData = EditorGUILayout.ObjectField(JsonData, typeof(TextAsset), false) as TextAsset;
        GUILayout.Label("Select Root Canvas");
        root = EditorGUILayout.ObjectField(root, typeof(GameObject), true) as GameObject;
        SpriteAtlasLoaded = EditorGUILayout.ObjectField(SpriteAtlasLoaded, typeof(SpriteAtlas), true) as SpriteAtlas;
        isLoadedSprite = EditorGUILayout.ObjectField(isLoadedSprite, typeof(Sprite), true) as Sprite;
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Load Sprite form Json"))
        {
            Sprite test= (Sprite)AssetDatabase.LoadAssetAtPath(AssetDatabase.FindAssets("LoadSpriteTest1")[0], typeof(Sprite));

            if (root!=null) rootObject = root.transform;

            // 기본 JSON 로딩  
            // JsonSpriteLib/SpriteLib = Sprite라이브러리 
            // getData/currentObj = PSD에서 로딩한 레이아웃 정보

            JsonSpriteLib = (TextAsset)AssetDatabase.LoadAssetAtPath("Assets/Art/UI_Sprite_DataBaseTest.json", typeof(TextAsset));
            GetTreeJson getData = JsonUtility.FromJson<GetTreeJson>(JsonData.text);
            GetTreeJson currentObj = getData;
            SpriteLibDB SpriteLib = JsonUtility.FromJson<SpriteLibDB>(JsonSpriteLib.text);

            SIdx = SpriteLib.Idx.ToArray();  // 단지 이름이 너무 길어서...


            // 하이어라키에 이벤트 시스템 체크하고 없으면 생성해 놓음
            InitEventSystem();

            // 첫번째 오브젝트 단계에서만  Canvas 속성을 체크합니다.
            // 캔버스가 최상단이 아닌 경우는 없음.
            // 만약 포토샵에서 로딩한 데이터의 최상단이 Canvas 인 경우 기존 Canvas는 무시하고
            // 새로운 Canvas를 생성해서 나머지를 붙여나갑니다. 
            // 포토샵 파일 한개당 두개이상의 캔버스는 만들지 않는걸로
            // 또한, 포토샵 파일 한개당 루트 오브젝트도 하나만 만들자.
            // 두개 이상 병렬 루트가 필요하면, 
            // 패널 한개를 임시로 만들어 모두 자식으로 등록하도록 가이드 할것.

            if (currentObj.Type == "Canvas")
            {
                GameObject gg = new GameObject();
                gg = SetAssetDefaultSetting(gg, null, currentObj);
                rootObject = gg.transform;
            }
            else
            {
                if (rootObject == null) rootObject = CheckRootCanvas();
                GameObject gg = new GameObject();
                gg = SetAssetDefaultSetting(gg, rootObject, currentObj);
                rootObject = gg.transform;
            }
            
            if (currentObj.HaveChild==true)
            {
                for (int i=0;i<currentObj.ChildsCount; i++)
                {
                    SetChild(rootObject, currentObj.Childrens[i]);
                }
            }                             
        }
        scrollPos = new Vector2(0, 100);
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        if (GUILayout.Button("Parse Sprite Atlas"))
        {
            
            Debug.Log("SCP=" + scrollPos);
            System.Type targetType = typeof(SpriteAtlas);
            UnityEngine.Object[] allAtlas = Resources.FindObjectsOfTypeAll(targetType);
            foreach (UnityEngine.Object atlass in allAtlas)
            {
                if (GUILayout.Button(atlass.name))
                {
                    Selection.activeObject = atlass;
                }
                Debug.Log(atlass.name);
            }           
            
        }
        EditorGUILayout.EndScrollView();
        rootObject = null;
    }
    
    Transform CheckRootCanvas()
    {
        // 기존에 Canvas 라는 이름의, Canvas 컴포넌트 오브젝트가 있으면 거기다 달고
        // 기존에 Canvas 라는 이름에 컴포넌트가 없거나, 이름자체가 없으면
        // 새 오브젝트로 Canvas 를 생성합니다.  

        GameObject isCanvas = GameObject.Find("Canvas");
        if (isCanvas!=null)
        {
            Canvas haveCanvas = isCanvas.GetComponent<Canvas>();
            if (haveCanvas == null) isCanvas=AddCanvas();
        }
        else 
        {
            isCanvas = AddCanvas();
        }
        


        return isCanvas.transform;
    }

    GameObject AddCanvas()
    {
        GameObject gg = new GameObject();
        RectTransform rt = gg.AddComponent<RectTransform>();
        var canvas = gg.AddComponent<Canvas>();
        gg.name = "Canvas";
        rt.anchoredPosition = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 1);
        rt.anchorMin = new Vector2(0, 0);
        gg.AddComponent<GraphicRaycaster>();
        var Scaler = gg.AddComponent<CanvasScaler>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = Camera.main;
        canvas.pixelPerfect = false;
        Scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        Scaler.referenceResolution = new Vector2(1280, 720);
        return gg;
    }

    void InitEventSystem()
    {
        if (FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<StandaloneInputModule>();
        }
    }

    void SetChild(Transform ParentObj,GetTreeJson currentObj)
    {
        GameObject gg = new GameObject();
        gg = SetAssetDefaultSetting(gg,ParentObj,currentObj);
        if (currentObj.HaveChild == true)
        {
            for (int i = 0; i < currentObj.ChildsCount; i++)
            {
                SetChild(gg.transform, currentObj.Childrens[i]);
            }
        }
    }
    GameObject SetAssetDefaultSetting(GameObject gg, Transform ParentObj, GetTreeJson currentObj)
    {
        gg.name = currentObj.Name;
        gg.transform.SetParent(ParentObj);
        RectTransform rt=gg.AddComponent<RectTransform>();
        switch (currentObj.Type)
        {
            case "Button" :
                gg.AddComponent<Image>();
                gg.AddComponent<Button>();
                SetRectTransform(rt, currentObj);
                break;
            case "Image":
                gg.AddComponent<Image>();
                SetRectTransform(rt, currentObj);
                break;
            case "Text":
                gg.AddComponent<Text>();
                SetRectTransform(rt, currentObj);
                break;
            case "Toggle":
                gg.AddComponent<Toggle>();
                SetRectTransform(rt, currentObj);
                break;
            case "Slider":
                gg.AddComponent<Slider>();
                SetRectTransform(rt, currentObj);
                break;
            case "Scrollbar":
                gg.AddComponent<Image>();
                SetRectTransform(rt, currentObj);
                gg.AddComponent<Scrollbar>();
                break;
            case "Dropdown":
                gg.AddComponent<Image>();
                SetRectTransform(rt, currentObj);
                gg.AddComponent<Dropdown>();
                break;
            case "InputField":
                gg.AddComponent<Image>();
                SetRectTransform(rt, currentObj);
                gg.AddComponent<InputField>();
                break;
            case "Panel":
                var ImageComponent=gg.AddComponent<Image>();
                break;
            case "Canvas":
                var canvas=gg.AddComponent<Canvas>();
                SetRectTransform(rt, currentObj);
                gg.AddComponent<GraphicRaycaster>();
                var Scaler = gg.AddComponent<CanvasScaler>();                
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = Camera.main;
                canvas.pixelPerfect = false;
                Scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                Scaler.referenceResolution = new Vector2(1280, 720);                
                break;
            case "ScrollView":
                gg.AddComponent<Image>();
                gg.AddComponent<ScrollRect>();
                SetRectTransform(rt, currentObj);
                var vp = new GameObject("Viewport");
                var vprt=vp.AddComponent<RectTransform>();
                vp.transform.parent = gg.transform;

                vprt.anchorMax = new Vector2(1, 1);
                vprt.anchorMin = new Vector2(0, 0);
                vprt.pivot = new Vector2(0.5f, 0.5f);
                break;
        }
        
        return gg;
    }
    void SetRectTransform(RectTransform rt, GetTreeJson currentObj)
    {
        rt.anchoredPosition3D = new Vector3(currentObj.Pos[0], currentObj.Pos[1],1f);
        
        rt.localScale = new Vector3(1, 1, 1);
        rt.sizeDelta = new Vector2(currentObj.Width, currentObj.Height);
        rt.anchorMax = new Vector2(AnchorValue[(int)currentObj.Anchor][2], AnchorValue[(int)currentObj.Anchor][3]);
        rt.anchorMin = new Vector2(AnchorValue[(int)currentObj.Anchor][0], AnchorValue[(int)currentObj.Anchor][1]);
        rt.pivot = PivotValue[(int)currentObj.Pivot];
    }

}
