using CossacksRTS.Input;
using CossacksRTS.Net;
using CossacksRTS.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class RtsDevSceneSetup
{
    private const string SceneName = "Dev_InputTest_2D";
    private const string ScenePath = "Assets/" + SceneName + ".unity";

    [MenuItem("Tools/CossacksRTS/Create 2D Input Test Scene")]
    public static void Create2DInputTestScene()
    {
        WarnIfMissingRequiredLayers();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var mainCameraGo = CreateGameObject("Main Camera");
        var mainCamera = mainCameraGo.AddComponent<Camera>();
        mainCamera.orthographic = true;
        mainCamera.orthographicSize = 10f;
        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = new Color(0.12f, 0.12f, 0.14f, 1f);
        mainCameraGo.transform.position = new Vector3(0f, 0f, -10f);
        mainCameraGo.tag = "MainCamera";

        var groundGo = CreateGameObject("Ground");
        groundGo.transform.position = Vector3.zero;
        groundGo.transform.localScale = new Vector3(60f, 60f, 1f);
        var groundSr = groundGo.AddComponent<SpriteRenderer>();
        groundSr.sprite = LoadBuiltinSprite("UI/Skin/UISprite.psd");
        groundSr.sortingOrder = -10;
        groundGo.AddComponent<BoxCollider2D>();

        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0)
        {
            groundGo.layer = groundLayer;
        }

        var unitGo = CreateGameObject("Unit_1");
        unitGo.transform.position = new Vector3(0f, 0f, 0f);
        unitGo.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
        var unitSr = unitGo.AddComponent<SpriteRenderer>();
        unitSr.sprite = LoadBuiltinSprite("UI/Skin/Knob.psd") ?? LoadBuiltinSprite("UI/Skin/UISprite.psd");
        unitSr.color = new Color(0.35f, 0.85f, 0.35f, 1f);
        unitGo.AddComponent<CircleCollider2D>();
        var selectable = unitGo.AddComponent<SelectableEntity>();

        int selectableLayer = LayerMask.NameToLayer("Selectable");
        if (selectableLayer >= 0)
        {
            unitGo.layer = selectableLayer;
        }

        // Set private serialized fields on SelectableEntity.
        var selectableSo = new SerializedObject(selectable);
        selectableSo.FindProperty("entityId").intValue = 1;
        selectableSo.FindProperty("ownerPlayerId").intValue = 0;
        selectableSo.ApplyModifiedPropertiesWithoutUndo();

        var selectionManagerGo = CreateGameObject("SelectionManager");
        var selectionManager = selectionManagerGo.AddComponent<SelectionManager>();
        var selectionSo = new SerializedObject(selectionManager);
        selectionSo.FindProperty("worldCamera").objectReferenceValue = mainCamera;
        selectionSo.ApplyModifiedPropertiesWithoutUndo();

        var gameSystemsGo = CreateGameObject("GameSystems");
        var commandBuffer = gameSystemsGo.AddComponent<CommandBuffer>();
        var uiBlocker = gameSystemsGo.AddComponent<UIBlocker>();
        var rtsInputController = gameSystemsGo.AddComponent<RtsInputController>();

        var canvasGo = CreateGameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>();
        var canvasRaycaster = canvasGo.AddComponent<GraphicRaycaster>();

        var panelGo = CreateGameObject("TopPanel", canvasGo.transform);
        var panelImage = panelGo.AddComponent<Image>();
        panelImage.color = new Color(0.08f, 0.08f, 0.08f, 0.85f);
        var panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(0f, 70f);

        // Ensure EventSystem exists.
        var eventSystem = Object.FindObjectOfType<EventSystem>();
        if (eventSystem == null)
        {
            var eventSystemGo = CreateGameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<StandaloneInputModule>();
        }

        // Wire UIBlocker references to canvas raycaster.
        var uiBlockerSo = new SerializedObject(uiBlocker);
        var raycastersProp = uiBlockerSo.FindProperty("blockingRaycasters");
        raycastersProp.arraySize = 1;
        raycastersProp.GetArrayElementAtIndex(0).objectReferenceValue = canvasRaycaster;
        uiBlockerSo.ApplyModifiedPropertiesWithoutUndo();

        // Wire RtsInputController references.
        var inputSo = new SerializedObject(rtsInputController);
        inputSo.FindProperty("worldCamera").objectReferenceValue = mainCamera;
        inputSo.FindProperty("selectionManager").objectReferenceValue = selectionManager;
        inputSo.FindProperty("commandBuffer").objectReferenceValue = commandBuffer;
        inputSo.FindProperty("uiBlocker").objectReferenceValue = uiBlocker;

        // Restrict command raycast to Ground + Selectable when both exist; fallback to all layers.
        int commandMask = ~0;
        if (groundLayer >= 0 && selectableLayer >= 0)
        {
            commandMask = (1 << groundLayer) | (1 << selectableLayer);
        }

        inputSo.FindProperty("commandRaycastMask").intValue = commandMask;
        inputSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
        {
            Debug.LogWarning($"[RtsDevSceneSetup] Failed to save scene at path: {ScenePath}");
        }
        else
        {
            Debug.Log($"[RtsDevSceneSetup] Created scene: {ScenePath}");
        }

        Selection.activeGameObject = gameSystemsGo;
        EditorGUIUtility.PingObject(gameSystemsGo);
    }

    private static void WarnIfMissingRequiredLayers()
    {
        bool missingGround = LayerMask.NameToLayer("Ground") < 0;
        bool missingSelectable = LayerMask.NameToLayer("Selectable") < 0;

        if (missingGround || missingSelectable)
        {
            Debug.LogWarning(
                "[RtsDevSceneSetup] Required layers are missing. Please create layers \"Ground\" and \"Selectable\" " +
                "in Project Settings > Tags and Layers. This utility will not modify TagManager automatically.");
        }
    }

    private static GameObject CreateGameObject(string name, Transform parent = null)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        if (parent != null)
        {
            Undo.SetTransformParent(go.transform, parent, $"Parent {name}");
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
        }

        return go;
    }

    private static Sprite LoadBuiltinSprite(string resourcePath)
    {
        // Example paths:
        // - UI/Skin/UISprite.psd
        // - UI/Skin/Knob.psd
        return AssetDatabase.GetBuiltinExtraResource<Sprite>(resourcePath);
    }
}
