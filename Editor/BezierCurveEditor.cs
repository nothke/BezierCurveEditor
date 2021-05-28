using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

using Nothke.Utils;

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
    int lastSelectedPointsCt;

    Quaternion multieditRotation = Quaternion.identity;
    Quaternion lastRotation = Quaternion.identity;
    Vector3 multieditScale = Vector3.one;
    Vector3 lastScale = Vector3.one;

    void OnEnable()
    {
        curve = (BezierCurve)target;

        resolutionProp = serializedObject.FindProperty("resolution");
        closeProp = serializedObject.FindProperty("_close");
        pointsProp = serializedObject.FindProperty("points");
        colorProp = serializedObject.FindProperty("drawColor");
        mirrorProp = serializedObject.FindProperty("_mirror");
        mirrorAxisProp = serializedObject.FindProperty("_axis");

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
        lastSelectedPointsCt = 0;

        Tools.hidden = false;
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
            for (int i = 0; i < curve.pointCount; i++)
            {
                if (curve[i] == null)
                {
                    Debug.LogWarning("Point is missing, please clean up manually");
                    continue;
                }

                DrawPointInspector(curve[i], i);
            }

            //if (GUILayout.Button("Add Point"))
            //    AddPoint();
        }

        toolMode = (ToolMode)GUILayout.SelectionGrid((int)toolMode, toolModesText, 3);

        if (toolMode != lastToolMode)
        {
            if (toolMode == ToolMode.Editing)
                Tools.hidden = true;
            else
                ExitEditMode();
        }

        lastToolMode = toolMode;

        #region Tool operators

        if (selectedPoints.Count < 2) GUI.enabled = false;
        if (GUILayout.Button("Align"))
        {
            RegisterPointsAndTransforms("Align Points");
            AlignPoints();
        }
        if (GUILayout.Button("Subdivide"))
        {
            Subdivide();
        }
        if (selectedPoints.Count < 2) GUI.enabled = true;

        if (selectedPoints.Count < 1) GUI.enabled = false;
        if (GUILayout.Button("Remove"))
        {
            RemovePoints();
        }
        if (selectedPoints.Count < 1) GUI.enabled = true;

        #endregion

        EditorGUILayout.PropertyField(mirrorProp);

        if (curve.mirror)
        {
            EditorGUILayout.PropertyField(mirrorAxisProp);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Snap to X"))
        {
            RegisterPointsAndTransforms("Snap to X");
            curve.SnapAllNodesToAxis(BezierCurve.Axis.X);
        }
        if (GUILayout.Button("Snap to Y"))
        {
            RegisterPointsAndTransforms("Snap to Y");
            curve.SnapAllNodesToAxis(BezierCurve.Axis.Y);
        }
        if (GUILayout.Button("Snap to Z"))
        {
            RegisterPointsAndTransforms("Snap to Z");
            curve.SnapAllNodesToAxis(BezierCurve.Axis.Z);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Mirror X"))
        {
            RegisterPointsAndTransforms("Mirror Around X");
            curve.MirrorAllNodesAroundAxis(BezierCurve.Axis.X);
        }
        if (GUILayout.Button("Mirror Y"))
        {
            RegisterPointsAndTransforms("Mirror Around Y");
            curve.MirrorAllNodesAroundAxis(BezierCurve.Axis.Y);
        }
        if (GUILayout.Button("Mirror Z"))
        {
            RegisterPointsAndTransforms("Mirror Around Z");
            curve.MirrorAllNodesAroundAxis(BezierCurve.Axis.Z);
        }
        EditorGUILayout.EndHorizontal();



        if (GUILayout.Button("Center Pivot"))
        {
            CenterPivot();
        }

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

    void RegisterPointsAndTransforms(string message)
    {
        Undo.RecordObject(curve, message);
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

                    createDragging = true;

                    curve.AddPointAt(targetPoint);
                }
                else if (Event.current.type == EventType.MouseUp)
                {
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

            if (sct != lastSelectedPointsCt)
            {
                Repaint();
            }

            lastSelectedPointsCt = sct;

            if (selectedPoints.Count > 0)
            {
                if (Tools.current == Tool.Move)
                {
                    Vector3 targetPos = Handles.PositionHandle(avgPosition, Quaternion.identity);

                    Vector3 diff = avgPosition - targetPos;

                    if (diff != Vector3.zero)
                    {
                        Undo.RecordObject(curve, "Move Points");
                        for (int sp = 0; sp < sct; sp++)
                        {
                            int i = selectedPoints[sp];
                            curve[i].position -= diff;
                        }
                    }
                }
                else if (Tools.current == Tool.Rotate)
                {
                    if (Event.current.button == 0 && Event.current.type == EventType.MouseUp)
                    {
                        multieditRotation = Quaternion.identity;
                        lastRotation = Quaternion.identity;
                    }

                    multieditRotation = Handles.RotationHandle(multieditRotation, avgPosition);


                    Quaternion rotDiff = multieditRotation * Quaternion.Inverse(lastRotation);

                    lastRotation = multieditRotation;

                    if (rotDiff != Quaternion.identity)
                    {
                        Undo.RecordObject(curve, "Rotate Points");
                        for (int sp = 0; sp < sct; sp++)
                        {
                            int i = selectedPoints[sp];

                            Vector3 posDiff = curve[i].position - avgPosition;
                            Vector3 newPos = rotDiff * posDiff;

                            curve[i].position = avgPosition + newPos;
                            curve[i].handle1 = rotDiff * curve[i].handle1;
                        }
                    }
                }
                else if (Tools.current == Tool.Scale)
                {
                    if (Event.current.button == 0 && Event.current.type == EventType.MouseUp)
                    {
                        multieditScale = Vector3.one;
                    }
                    else
                    {
                        multieditScale = Handles.ScaleHandle(multieditScale, avgPosition, Quaternion.identity, HandleUtility.GetHandleSize(avgPosition));

                        Vector3 scaleDiff = multieditScale - lastScale;
                        lastScale = multieditScale;

                        Vector3 scaleMult = Vector3.one + scaleDiff;

                        if (scaleDiff != Vector3.zero && multieditScale != Vector3.one)
                        {
                            Undo.RecordObject(curve, "Scale Points");

                            for (int sp = 0; sp < sct; sp++)
                            {
                                int i = selectedPoints[sp];

                                Vector3 posDiff = curve[i].position - avgPosition;
                                Vector3 newPos = Vector3.Scale(posDiff, scaleMult);
                                curve[i].position = avgPosition + newPos;

                                curve[i].globalHandle1 = avgPosition + Vector3.Scale(curve[i].globalHandle1 - avgPosition, scaleMult);
                                curve[i].globalHandle2 = avgPosition + Vector3.Scale(curve[i].globalHandle2 - avgPosition, scaleMult);
                            }
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

                    multieditRotation = Quaternion.identity;
                    lastRotation = Quaternion.identity;

                    regionSelect = false;
                }
            }
        }

        blockSelection = toolMode != ToolMode.None;
    }

    void DrawPointInspector(CurvePoint point, int index)
    {
        if (point == null)
        {
            RemovePoint(index);
            return;
        }

        SerializedProperty pointsArray = serializedObject.FindProperty("points");

        if (index >= pointsArray.arraySize)
            return;

        SerializedProperty serObj = pointsArray.GetArrayElementAtIndex(index);

        SerializedProperty handleStyleProp = serObj.FindPropertyRelative("handleStyle");
        SerializedProperty positionProp = serObj.FindPropertyRelative("_position");
        SerializedProperty handle1Prop = serObj.FindPropertyRelative("_handle1");
        SerializedProperty handle2Prop = serObj.FindPropertyRelative("_handle2");

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("X", GUILayout.Width(20)))
        {
            RemovePoint(index);
            return;
        }

        GUILayout.Label("Point " + index);

        if (index != 0 && GUILayout.Button(@"/\", GUILayout.Width(25)))
        {
            pointsProp.MoveArrayElement(index, index - 1);
            curve.SetDirty();
        }

        if (index != pointsProp.arraySize - 1 && GUILayout.Button(@"\/", GUILayout.Width(25)))
        {
            pointsProp.MoveArrayElement(index, index + 1);
            curve.SetDirty();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUI.indentLevel++;
        EditorGUI.indentLevel++;

        int newType = (int)((object)EditorGUILayout.EnumPopup("Handle Type", (CurvePoint.HandleStyle)handleStyleProp.enumValueIndex));

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

        EditorGUILayout.PropertyField(positionProp);

        // CONNECTED bezier type
        if (handleStyleProp.enumValueIndex == 0)
        {
            // Makes sure that handles are reflected when manipulated in inspector
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(handle1Prop);
            if (EditorGUI.EndChangeCheck())
            {
                handle2Prop.vector3Value = -handle1Prop.vector3Value;
            }

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(handle2Prop);
            if (EditorGUI.EndChangeCheck())
            {
                handle1Prop.vector3Value = -handle2Prop.vector3Value;
            }
        }

        // BROKEN bezier type
        else if (handleStyleProp.enumValueIndex == 1)
        {
            EditorGUILayout.PropertyField(handle1Prop);
            EditorGUILayout.PropertyField(handle2Prop);
        }

        EditorGUI.indentLevel--;
        EditorGUI.indentLevel--;

        if (GUI.changed)
        {
            serializedObject.ApplyModifiedProperties();
        }
    }

    void RemovePoint(int index)
    {
        pointsProp.MoveArrayElement(index, curve.pointCount - 1);
        pointsProp.arraySize--;

        curve.SetDirty();

        Undo.RecordObject(target, "Remove Point");

        return;
    }

    static void DrawPointSceneGUI(CurvePoint point, int index)
    {
        Handles.color = Color.green;

        // While using a live tool, the selection is disabled because it messes with controlId of the tool
        // Replace with better selection system
        if (blockSelection)
        {
            Vector3 newPosition = Handles.FreeMoveHandle(point.position, Quaternion.identity, HandleUtility.GetHandleSize(point.position) * 0.1f, Vector3.zero, Handles.RectangleHandleCap);

            if (newPosition != point.position)
            {
                Undo.RecordObject(point.curve, "Move Point");
                point.position = newPosition;
            }
        }
        else
        {
            // Use control id to figure out which object is currently being controlled. Is there a better method?
            int ctrlId = GUIUtility.GetControlID(FocusType.Passive);
            Vector3 newPosition = Handles.FreeMoveHandle(ctrlId, point.position, Quaternion.identity, HandleUtility.GetHandleSize(point.position) * 0.1f, Vector3.zero, Handles.RectangleHandleCap);

            if (newPosition != point.position)
            {
                Undo.RecordObject(point.curve, "Move Point");
                point.position = newPosition;
            }
            else if (GUIUtility.hotControl == ctrlId)
            {
                point.curve.lastClickedPointIndex = index;
            }
        }

        if (point.handleStyle != CurvePoint.HandleStyle.None)
        {
            Handles.color = Color.cyan;
            Vector3 newGlobal1 = Handles.FreeMoveHandle(point.globalHandle1, Quaternion.identity, HandleUtility.GetHandleSize(point.globalHandle1) * 0.075f, Vector3.zero, Handles.CircleHandleCap);
            if (point.globalHandle1 != newGlobal1)
            {
                Undo.RecordObject(point.curve, "Move Handle");
                point.globalHandle1 = newGlobal1;
                if (point.handleStyle == CurvePoint.HandleStyle.Connected) point.globalHandle2 = -(newGlobal1 - point.position) + point.position;
            }

            Vector3 newGlobal2 = Handles.FreeMoveHandle(point.globalHandle2, Quaternion.identity, HandleUtility.GetHandleSize(point.globalHandle2) * 0.075f, Vector3.zero, Handles.CircleHandleCap);
            if (point.globalHandle2 != newGlobal2)
            {
                Undo.RecordObject(point.curve, "Move Handle");
                point.globalHandle2 = newGlobal2;
                if (point.handleStyle == CurvePoint.HandleStyle.Connected) point.globalHandle1 = -(newGlobal2 - point.position) + point.position;
            }

            Handles.color = Color.yellow;
            Handles.DrawLine(point.position, point.globalHandle1);
            Handles.DrawLine(point.position, point.globalHandle2);
        }

        {
            Vector2 screenPos = HandleUtility.WorldToGUIPoint(point.position);
            screenPos.x += 12;
            screenPos.y -= 5;

            var screenRay = HandleUtility.GUIPointToWorldRay(screenPos);
            var p = screenRay.GetPoint(1);

            Handles.Label(p, index.ToString());
        }
    }

    public static void DrawOtherPoints(BezierCurve curve, CurvePoint caller)
    {
        if (!curve) return;

        foreach (CurvePoint p in curve.GetAnchorPoints())
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

        CurvePoint p1 = curve.AddPointAt(Vector3.forward * 0.5f);
        p1.handleStyle = CurvePoint.HandleStyle.Connected;
        p1.handle1 = new Vector3(-0.28f, 0, 0);

        CurvePoint p2 = curve.AddPointAt(Vector3.right * 0.5f);
        p2.handleStyle = CurvePoint.HandleStyle.Connected;
        p2.handle1 = new Vector3(0, 0, 0.28f);

        CurvePoint p3 = curve.AddPointAt(-Vector3.forward * 0.5f);
        p3.handleStyle = CurvePoint.HandleStyle.Connected;
        p3.handle1 = new Vector3(0.28f, 0, 0);

        CurvePoint p4 = curve.AddPointAt(-Vector3.right * 0.5f);
        p4.handleStyle = CurvePoint.HandleStyle.Connected;
        p4.handle1 = new Vector3(0, 0, -0.28f);

        curve.close = true;
    }

    void CenterPivot()
    {
        if (curve.pointCount == 0)
            return;

        Undo.RecordObject(target, "Center Pivot");

        Bounds bounds = new Bounds(curve[0].position, Vector3.zero);

        for (int i = 1; i < curve.pointCount; i++)
        {
            bounds.Encapsulate(curve[i].position);
        }

        Vector3 targetPosition = bounds.center;

        Vector3 offset = targetPosition - curve.transform.position;

        SerializedObject curveTransformSO = new SerializedObject(curve.transform);
        curveTransformSO.FindProperty("m_LocalPosition").vector3Value = targetPosition;

        for (int i = 0; i < curve.pointCount; i++)
        {
            Vector3 position = curve[i].localPosition - offset;

            var pointProp = pointsProp.GetArrayElementAtIndex(i);
            var posProp = pointProp.FindPropertyRelative("_position");

            posProp.vector3Value = position;
        }

        Undo.RecordObject(curve, "Center Pivot");
        curveTransformSO.ApplyModifiedProperties();
    }

    void RemovePoints()
    {
        selectedPoints.Sort();

        for (int sp = selectedPoints.Count - 1; sp >= 0; sp--)
        {
            int i = selectedPoints[sp];
            pointsProp.DeleteArrayElementAtIndex(i);
        }

        Undo.RecordObject(curve, "Remove Points");
        serializedObject.ApplyModifiedProperties();

        selectedPoints.Clear();
    }

    void AlignPoints()
    {
        selectedPoints.Sort();

        Vector3 median = Vector3.zero;
        List<CurvePoint> points = new List<CurvePoint>(curve.pointCount);
        for (int sp = 0; sp < selectedPoints.Count; sp++)
        {
            int i = selectedPoints[sp];
            points.Add(curve[i]);

            Vector3 pos = curve[i].position;

            median += pos / selectedPoints.Count;
        }

        Vector3 normal = Vector3.Normalize(points[points.Count - 1].position - points[0].position);
        median = (points[0].position + points[points.Count - 1].position) / 2;

        for (int i = 0; i < points.Count; i++)
        {
            Quaternion rot = Quaternion.FromToRotation(points[i].handle2.normalized, normal);
            points[i].handle2 = rot * points[i].handle2;
        }

        if (points.Count > 2)
        {
            for (int i = 1; i < points.Count - 1; i++)
            {
                points[i].position = Vector3.Project(points[i].position - median, normal) + median;
            }
        }

        //Debug.DrawRay(points[0].position, normal * 100, Color.yellow, 1);

        //Debug.DrawRay(median, Vector3.forward * 100, Color.red, 1);
    }

    void Subdivide()
    {
        selectedPoints.Sort();

        if (selectedPoints[0] != selectedPoints[1] - 1)
        {
            Debug.LogWarning("Cannot subdivide non sequential points");
            return;
        }

        Undo.RecordObject(curve, "Subdivide");

        CurvePoint point1 = curve[selectedPoints[0]];
        CurvePoint point2 = curve[selectedPoints[1]];

        BezierUtility.SplitBezier(0.5f,
            point1.position, point2.position,
            point1.globalHandle2, point2.globalHandle1,
            out Vector3 leftStartPosition, out Vector3 leftEndPosition,
            out Vector3 leftStartTangent, out Vector3 leftEndTangent,
            out Vector3 rightStartPosition, out Vector3 rightEndPosition,
            out Vector3 rightStartTangent, out Vector3 rightEndTangent);

        CurvePoint newPoint = new CurvePoint(curve)
        {
            handleStyle = CurvePoint.HandleStyle.Connected,

            position = leftEndPosition,
            globalHandle1 = leftEndTangent,
            globalHandle2 = rightStartTangent
        };

        int index = selectedPoints[0] + 1;
        pointsProp.InsertArrayElementAtIndex(index);
        curve[index].position = leftEndPosition;
        curve[index].globalHandle1 = leftEndTangent;
        curve[index].globalHandle2 = rightStartTangent;

        var prop = pointsProp.GetArrayElementAtIndex(index);
        prop.FindPropertyRelative("_position").vector3Value = curve[index].localPosition;
        prop.FindPropertyRelative("_handle1").vector3Value = curve[index].handle1;
        prop.FindPropertyRelative("_handle2").vector3Value = curve[index].handle2;
        serializedObject.ApplyModifiedProperties();
    }

    bool GetMouseSceneHit(out RaycastHit hit)
    {
        Vector2 guiPosition = Event.current.mousePosition;
        Ray ray = HandleUtility.GUIPointToWorldRay(guiPosition);

        return Physics.Raycast(ray, out hit);
    }
}
