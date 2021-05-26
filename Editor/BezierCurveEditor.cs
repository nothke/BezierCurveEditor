using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BezierCurve))]
public class BezierCurveEditor : Editor
{
    BezierCurve curve;
    SerializedProperty resolutionProp;
    SerializedProperty closeProp;
    SerializedProperty pointsProp;
    SerializedProperty colorProp;
    SerializedProperty mirrorProp;
    SerializedProperty mirrorAxisProp;

    private static bool showPointsFoldout = true;

    EditorApplication.CallbackFunction delayRemoveDelegate;
    BezierPoint pointToDestroy;

    // Tool
    enum ToolMode { None, Creating, Editing };
    ToolMode toolMode;
    ToolMode lastToolMode = ToolMode.None;
    bool createDragging;

    static readonly string[] toolModesText = { "None", "Add", "Multiedit" };

    static bool blockSelection;

    // Multiediting
    Vector2 selectionStartPos;
    bool regionSelect;
    List<int> selectedPoints;

    void OnEnable()
    {
        curve = (BezierCurve)target;

        resolutionProp = serializedObject.FindProperty("resolution");
        closeProp = serializedObject.FindProperty("_close");
        pointsProp = serializedObject.FindProperty("points");
        colorProp = serializedObject.FindProperty("drawColor");
        mirrorProp = serializedObject.FindProperty("_mirror");
        mirrorAxisProp = serializedObject.FindProperty("_axis");

        delayRemoveDelegate = new EditorApplication.CallbackFunction(RemovePoint);

        createDragging = false;

        selectedPoints = new List<int>();

        if (toolMode == ToolMode.Editing)
            Tools.hidden = true;
        else
            ExitEditMode();
    }

    private void OnDisable()
    {
        blockSelection = false;

        ExitEditMode();
    }

    void ExitEditMode()
    {
        selectedPoints.Clear();

        Tools.hidden = false;
    }

    void RemovePoint()
    {
        Undo.DestroyObjectImmediate(pointToDestroy.gameObject);
        EditorApplication.delayCall -= delayRemoveDelegate;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        if (curve.resolution < 0.0001f) curve.resolution = 0.0001f;

        EditorGUILayout.PropertyField(resolutionProp);
        EditorGUILayout.PropertyField(closeProp);
        EditorGUILayout.PropertyField(colorProp);
        BezierCurve.drawInterpolatedPoints = GUILayout.Toggle(BezierCurve.drawInterpolatedPoints, "Draw Interpolated Points");

        showPointsFoldout = EditorGUILayout.Foldout(showPointsFoldout, "Points");

        if (showPointsFoldout)
        {
            int pointCount = pointsProp.arraySize;

            for (int i = 0; i < pointCount; i++)
            {
                if (curve[i] == null)
                {
                    Debug.LogWarning("Point is missing, please clean up manually");
                    continue;
                }

                DrawPointInspector(curve[i], i);
            }

            if (GUILayout.Button("Add Point"))
            {
                GameObject pointObject = new GameObject("Point " + pointsProp.arraySize);
                pointObject.transform.parent = curve.transform;

                Undo.RegisterCreatedObjectUndo(pointObject, "Add Point");

                Vector3 direction;
                if (pointCount >= 1)
                {
                    direction = (curve.GetAnchorPoints()[pointCount - 1].handle2 - curve.GetAnchorPoints()[pointCount - 1].handle1).normalized;
                    pointObject.transform.localPosition = curve.GetAnchorPoints()[pointCount - 1].localPosition + direction * 2;
                }
                else
                {
                    direction = Vector3.forward;
                    pointObject.transform.localPosition = Vector3.zero;
                }

                BezierPoint newPoint = pointObject.AddComponent<BezierPoint>();

                newPoint._curve = curve;
                newPoint.handle1 = -direction;
                newPoint.handle2 = direction;

                pointsProp.InsertArrayElementAtIndex(pointsProp.arraySize);
                pointsProp.GetArrayElementAtIndex(pointsProp.arraySize - 1).objectReferenceValue = newPoint;
            }

            toolMode = (ToolMode)GUILayout.SelectionGrid((int)toolMode, toolModesText, 3);
        }

        EditorGUILayout.PropertyField(mirrorProp);

        if (curve.mirror)
        {
            EditorGUILayout.PropertyField(mirrorAxisProp);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Snap to X"))
        {
            Transform[] bpObjects = new Transform[curve.pointCount];
            for (int i = 0; i < bpObjects.Length; i++)
                bpObjects[i] = curve[i].transform;

            Undo.RecordObjects(bpObjects, "Snap to Axis");
            curve.SnapAllNodesToAxis(BezierCurve.Axis.X);
        }
        if (GUILayout.Button("Snap to Y"))
        {
            Transform[] bpObjects = new Transform[curve.pointCount];
            for (int i = 0; i < bpObjects.Length; i++)
                bpObjects[i] = curve[i].transform;

            Undo.RecordObjects(bpObjects, "Snap to Axis");
            curve.SnapAllNodesToAxis(BezierCurve.Axis.Y);
        }
        if (GUILayout.Button("Snap to Z"))
        {
            Transform[] bpObjects = new Transform[curve.pointCount];
            for (int i = 0; i < bpObjects.Length; i++)
                bpObjects[i] = curve[i].transform;

            Undo.RecordObjects(bpObjects, "Snap to Axis");
            curve.SnapAllNodesToAxis(BezierCurve.Axis.Z);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Mirror X"))
        {
            Transform[] bpObjects = new Transform[curve.pointCount];
            for (int i = 0; i < bpObjects.Length; i++)
                bpObjects[i] = curve[i].transform;

            Undo.RecordObjects(bpObjects, "Mirror Around Axis");
            curve.MirrorAllNodesAroundAxis(BezierCurve.Axis.X);
        }
        if (GUILayout.Button("Mirror Y"))
        {
            Transform[] bpObjects = new Transform[curve.pointCount];
            for (int i = 0; i < bpObjects.Length; i++)
                bpObjects[i] = curve[i].transform;

            Undo.RecordObjects(bpObjects, "Mirror Around Axis");
            curve.MirrorAllNodesAroundAxis(BezierCurve.Axis.Y);
        }
        if (GUILayout.Button("Mirror Z"))
        {
            Transform[] bpObjects = new Transform[curve.pointCount];
            for (int i = 0; i < bpObjects.Length; i++)
                bpObjects[i] = curve[i].transform;

            Undo.RecordObjects(bpObjects, "Mirror Around Axis");
            curve.MirrorAllNodesAroundAxis(BezierCurve.Axis.Z);
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Clean-up null points"))
        {
            curve.CleanupNullPoints();
        }

        if (GUI.changed)
        {
            serializedObject.ApplyModifiedProperties();
            curve.SetDirty();
            EditorUtility.SetDirty(target);
        }
    }

    void OnSceneGUI()
    {
        for (int i = 0; i < curve.pointCount; i++)
        {
            DrawPointSceneGUI(curve[i], i);
        }

        if (toolMode == ToolMode.Creating)
        {

            Vector3 targetPoint;
            Vector3 targetNormal;
            if (GetMouseSceneHit(out RaycastHit hit))
            {
                targetPoint = hit.point;
                targetNormal = hit.normal;
            }
            else
            {
                Vector2 guiPosition = Event.current.mousePosition;
                Ray ray = HandleUtility.GUIPointToWorldRay(guiPosition);

                if (curve.pointCount > 0)
                {
                    Plane plane = new Plane(Vector3.up, curve.Last().position);

                    plane.Raycast(ray, out float d);
                    targetPoint = ray.GetPoint(d);
                }
                else
                {
                    Plane plane = new Plane(Vector3.up, curve.transform.position);

                    plane.Raycast(ray, out float d);
                    targetPoint = ray.GetPoint(d);
                }

                targetNormal = Vector3.up;
            }

            Handles.ArrowHandleCap(0,
                targetPoint, Quaternion.LookRotation(targetNormal, Vector3.forward),
                20, EventType.Repaint);

            SceneView.RepaintAll();

            if (createDragging)
            {
                curve[curve.pointCount - 1].globalHandle2 = targetPoint;
            }

            if (Event.current.button == 0)
            {
                if (Event.current.type == EventType.MouseDown)
                {
                    GUIUtility.hotControl = 0;
                    int controlId = GUIUtility.GetControlID(FocusType.Passive);

                    GUIUtility.hotControl = controlId;
                    Event.current.Use();

                    Debug.Log("Click down");
                    createDragging = true;

                    curve.AddPointAt(targetPoint);
                }
                else if (Event.current.type == EventType.MouseUp)
                {
                    //int controlId = GUIUtility.GetControlID(FocusType.Passive);
                    //GUIUtility.hotControl = controlId;
                    //Event.current.Use();

                    Debug.Log("Click up");
                    createDragging = false;
                }
            }
        }
        else if (toolMode == ToolMode.Editing)
        {
            int controlId = GUIUtility.GetControlID(FocusType.Passive);

            if (regionSelect)
            {
                var mousePos = Event.current.mousePosition;
                Rect selectionRect = new Rect(selectionStartPos, mousePos - selectionStartPos);

                selectedPoints.Clear();
                for (int i = 0; i < curve.pointCount; i++)
                {
                    var point = HandleUtility.WorldToGUIPoint(curve[i].position);
                    if (selectionRect.Contains(point))
                    {
                        selectedPoints.Add(i);
                    }
                }

                Handles.BeginGUI();
                GUI.Box(selectionRect, new GUIContent());
                Handles.EndGUI();

                SceneView.RepaintAll();
            }

            Vector3 avgPosition = Vector3.zero;

            int sct = selectedPoints.Count;
            for (int sp = 0; sp < sct; sp++)
            {
                int i = selectedPoints[sp];

                Vector3 pos = curve[i].position;
                avgPosition += pos / sct;

                float size = HandleUtility.GetHandleSize(pos) * 0.1f;
                Handles.SphereHandleCap(-1, pos, Quaternion.identity, size, EventType.Repaint);
            }

            if (selectedPoints.Count > 0)
            {
                if (Tools.current == Tool.Move)
                {
                    Vector3 targetPos = Handles.PositionHandle(avgPosition, Quaternion.identity);

                    Vector3 diff = avgPosition - targetPos;

                    if (diff != Vector3.zero)
                    {
                        for (int sp = 0; sp < sct; sp++)
                        {
                            int i = selectedPoints[sp];
                            curve[i].position -= diff;
                        }
                    }
                }
            }

            if (Event.current.button == 0)
            {
                if (Event.current.type == EventType.MouseDown)
                {
                    GUIUtility.hotControl = controlId;
                    Event.current.Use();

                    selectionStartPos = Event.current.mousePosition;

                    regionSelect = true;
                }
                else if (Event.current.type == EventType.MouseUp)
                {
                    //GUIUtility.hotControl = controlId;
                    //Event.current.Use();

                    regionSelect = false;
                }
            }
        }

        blockSelection = toolMode != ToolMode.None;
    }

    void DrawPointInspector(BezierPoint point, int index)
    {
        if (point == null)
        {
            Undo.RecordObject(target, "Remove Point");
            pointsProp.MoveArrayElement(curve.GetPointIndex(point), curve.pointCount - 1);
            pointsProp.arraySize--;
            curve.SetDirty();

            return;
        }

        SerializedObject serObj = new SerializedObject(point);

        SerializedProperty handleStyleProp = serObj.FindProperty("handleStyle");
        SerializedProperty handle1Prop = serObj.FindProperty("_handle1");
        SerializedProperty handle2Prop = serObj.FindProperty("_handle2");

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("X", GUILayout.Width(20)))
        {
            Undo.RecordObject(target, "Remove Point");
            pointsProp.MoveArrayElement(curve.GetPointIndex(point), curve.pointCount - 1);
            pointsProp.arraySize--;

            curve.SetDirty();

            EditorApplication.delayCall += delayRemoveDelegate;
            pointToDestroy = point;

            return;
        }

        EditorGUILayout.ObjectField(point.gameObject, typeof(GameObject), true);

        if (index != 0 && GUILayout.Button(@"/\", GUILayout.Width(25)))
        {
            UnityEngine.Object other = pointsProp.GetArrayElementAtIndex(index - 1).objectReferenceValue;
            pointsProp.GetArrayElementAtIndex(index - 1).objectReferenceValue = point;
            pointsProp.GetArrayElementAtIndex(index).objectReferenceValue = other;
            curve.SetDirty();
        }

        if (index != pointsProp.arraySize - 1 && GUILayout.Button(@"\/", GUILayout.Width(25)))
        {
            UnityEngine.Object other = pointsProp.GetArrayElementAtIndex(index + 1).objectReferenceValue;
            pointsProp.GetArrayElementAtIndex(index + 1).objectReferenceValue = point;
            pointsProp.GetArrayElementAtIndex(index).objectReferenceValue = other;
            curve.SetDirty();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUI.indentLevel++;
        EditorGUI.indentLevel++;

        int newType = (int)((object)EditorGUILayout.EnumPopup("Handle Type", (BezierPoint.HandleStyle)handleStyleProp.enumValueIndex));

        if (newType != handleStyleProp.enumValueIndex)
        {
            handleStyleProp.enumValueIndex = newType;
            if (newType == 0)
            {
                if (handle1Prop.vector3Value != Vector3.zero) handle2Prop.vector3Value = -handle1Prop.vector3Value;
                else if (handle2Prop.vector3Value != Vector3.zero) handle1Prop.vector3Value = -handle2Prop.vector3Value;
                else
                {
                    handle1Prop.vector3Value = new Vector3(0.1f, 0, 0);
                    handle2Prop.vector3Value = new Vector3(-0.1f, 0, 0);
                }
            }

            else if (newType == 1)
            {
                if (handle1Prop.vector3Value == Vector3.zero && handle2Prop.vector3Value == Vector3.zero)
                {
                    handle1Prop.vector3Value = new Vector3(0.1f, 0, 0);
                    handle2Prop.vector3Value = new Vector3(-0.1f, 0, 0);
                }
            }

            else if (newType == 2)
            {
                handle1Prop.vector3Value = Vector3.zero;
                handle2Prop.vector3Value = Vector3.zero;
            }

            curve.SetDirty();
        }

        Vector3 newPointPos = EditorGUILayout.Vector3Field("Position : ", point.localPosition);
        if (newPointPos != point.localPosition)
        {
            Undo.RecordObject(point.transform, "Move Bezier Point");
            point.localPosition = newPointPos;
        }

        if (handleStyleProp.enumValueIndex == 0)
        {
            Vector3 newPosition;

            newPosition = EditorGUILayout.Vector3Field("Handle 1", handle1Prop.vector3Value);
            if (newPosition != handle1Prop.vector3Value)
            {
                handle1Prop.vector3Value = newPosition;
                handle2Prop.vector3Value = -newPosition;
                curve.SetDirty();
            }

            newPosition = EditorGUILayout.Vector3Field("Handle 2", handle2Prop.vector3Value);
            if (newPosition != handle2Prop.vector3Value)
            {
                handle1Prop.vector3Value = -newPosition;
                handle2Prop.vector3Value = newPosition;
                curve.SetDirty();
            }
        }

        else if (handleStyleProp.enumValueIndex == 1)
        {
            EditorGUILayout.PropertyField(handle1Prop);
            EditorGUILayout.PropertyField(handle2Prop);
        }

        EditorGUI.indentLevel--;
        EditorGUI.indentLevel--;

        if (GUI.changed)
        {
            serObj.ApplyModifiedProperties();
            EditorUtility.SetDirty(serObj.targetObject);
        }
    }

    static void DrawPointSceneGUI(BezierPoint point, int index)
    {
        if (point == null)
        {
            Debug.LogWarning("Point is missing, please clean up manually");
            return;
        }

        Handles.Label(point.position + new Vector3(0, HandleUtility.GetHandleSize(point.position) * 0.4f, 0), point.gameObject.name);

        Handles.color = Color.green;

        // While using a live tool, the selection is disabled because it messes with controlId of the tool
        // Replace with better selection system
        if (blockSelection)
        {
            Vector3 newPosition = Handles.FreeMoveHandle(point.position, point.transform.rotation, HandleUtility.GetHandleSize(point.position) * 0.1f, Vector3.zero, Handles.RectangleHandleCap);

            if (newPosition != point.position)
            {
                Undo.RecordObject(point.transform, "Move Point");
                point.position = newPosition;
            }
        }
        else
        {
            // Use control id to figure out which object is currently being controlled. Is there a better method?
            int ctrlId = GUIUtility.GetControlID(FocusType.Passive);
            Vector3 newPosition = Handles.FreeMoveHandle(ctrlId, point.position, point.transform.rotation, HandleUtility.GetHandleSize(point.position) * 0.1f, Vector3.zero, Handles.RectangleHandleCap);

            if (newPosition != point.position)
            {
                Undo.RecordObject(point.transform, "Move Point");
                point.position = newPosition;
            }
            else if (GUIUtility.hotControl == ctrlId)
            {
                point.curve.lastClickedPointIndex = index;
            }
        }

        if (point.handleStyle != BezierPoint.HandleStyle.None)
        {
            Handles.color = Color.cyan;
            Vector3 newGlobal1 = Handles.FreeMoveHandle(point.globalHandle1, point.transform.rotation, HandleUtility.GetHandleSize(point.globalHandle1) * 0.075f, Vector3.zero, Handles.CircleHandleCap);
            if (point.globalHandle1 != newGlobal1)
            {
                Undo.RecordObject(point, "Move Handle");
                point.globalHandle1 = newGlobal1;
                if (point.handleStyle == BezierPoint.HandleStyle.Connected) point.globalHandle2 = -(newGlobal1 - point.position) + point.position;
            }

            Vector3 newGlobal2 = Handles.FreeMoveHandle(point.globalHandle2, point.transform.rotation, HandleUtility.GetHandleSize(point.globalHandle2) * 0.075f, Vector3.zero, Handles.CircleHandleCap);
            if (point.globalHandle2 != newGlobal2)
            {
                Undo.RecordObject(point, "Move Handle");
                point.globalHandle2 = newGlobal2;
                if (point.handleStyle == BezierPoint.HandleStyle.Connected) point.globalHandle1 = -(newGlobal2 - point.position) + point.position;
            }

            Handles.color = Color.yellow;
            Handles.DrawLine(point.position, point.globalHandle1);
            Handles.DrawLine(point.position, point.globalHandle2);
        }
    }

    public static void DrawOtherPoints(BezierCurve curve, BezierPoint caller)
    {
        if (!curve) return;

        foreach (BezierPoint p in curve.GetAnchorPoints())
        {
            if (p != caller) DrawPointSceneGUI(p, 0);
        }
    }

    [MenuItem("GameObject/Create Other/Bezier Curve")]
    public static void CreateCurve(MenuCommand command)
    {
        GameObject curveObject = new GameObject("BezierCurve");
        Undo.RecordObject(curveObject, "Undo Create Curve");
        BezierCurve curve = curveObject.AddComponent<BezierCurve>();

        BezierPoint p1 = curve.AddPointAt(Vector3.forward * 0.5f);
        p1.handleStyle = BezierPoint.HandleStyle.Connected;
        p1.handle1 = new Vector3(-0.28f, 0, 0);

        BezierPoint p2 = curve.AddPointAt(Vector3.right * 0.5f);
        p2.handleStyle = BezierPoint.HandleStyle.Connected;
        p2.handle1 = new Vector3(0, 0, 0.28f);

        BezierPoint p3 = curve.AddPointAt(-Vector3.forward * 0.5f);
        p3.handleStyle = BezierPoint.HandleStyle.Connected;
        p3.handle1 = new Vector3(0.28f, 0, 0);

        BezierPoint p4 = curve.AddPointAt(-Vector3.right * 0.5f);
        p4.handleStyle = BezierPoint.HandleStyle.Connected;
        p4.handle1 = new Vector3(0, 0, -0.28f);

        curve.close = true;
    }

    bool GetMouseSceneHit(out RaycastHit hit)
    {
        Vector2 guiPosition = Event.current.mousePosition;
        Ray ray = HandleUtility.GUIPointToWorldRay(guiPosition);

        return Physics.Raycast(ray, out hit);
    }
}
