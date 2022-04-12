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

    private static bool showPointsFoldout = false;

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

    static bool lockDirection = false;

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


        EditorGUILayout.PropertyField(mirrorProp, new GUIContent("Show Mirrored"));

        if (curve.mirror)
        {
            EditorGUILayout.PropertyField(mirrorAxisProp);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Zero X"))
        {
            RegisterPointsAndTransforms("Snap to X");
            curve.SnapAllNodesToAxis(BezierCurve.Axis.X);
        }
        if (GUILayout.Button("Zero Y"))
        {
            RegisterPointsAndTransforms("Snap to Y");
            curve.SnapAllNodesToAxis(BezierCurve.Axis.Y);
        }
        if (GUILayout.Button("Zero Z"))
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

        if (GUILayout.Button("Reverse"))
            Reverse();

        if (GUILayout.Button("Center Pivot"))
            CenterPivot();

        if (GUI.changed)
        {
            serializedObject.ApplyModifiedProperties();
            curve.SetDirty();
            EditorUtility.SetDirty(target);
        }
    }

    void RegisterPointsChanged()
    {
        curve.InvokeEndedMovingPoint();
    }

    void RegisterPointsAndTransforms(string message)
    {
        Undo.RegisterCompleteObjectUndo(curve, message);
    }

    void OnSceneGUI()
    {
        if (Event.current.type == EventType.MouseUp
            && Event.current.button == 0)
        {
            curve.InvokeEndedMovingPoint();
        }

        for (int i = 0; i < curve.pointCount; i++)
        {
            DrawPointSceneGUI(curve[i], i);
        }

        DrawSceneWindow();

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

                if (selectionRect.width < 0)
                {
                    selectionRect.x += selectionRect.width;
                    selectionRect.width = -selectionRect.width;
                }

                if (selectionRect.height < 0)
                {
                    selectionRect.y += selectionRect.height;
                    selectionRect.height = -selectionRect.height;
                }

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
                        Undo.RegisterCompleteObjectUndo(curve, "Move Points");
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
                        Undo.RegisterCompleteObjectUndo(curve, "Rotate Points");
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
                            Undo.RegisterCompleteObjectUndo(curve, "Scale Points");

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

    Rect toolWindowRect = new Rect(20, 40, 200, 0);

    void DrawSceneWindow()
    {
        int x = toolMode == ToolMode.Editing ? 200 : 80;

        toolWindowRect = GUILayout.Window(5324, toolWindowRect, SceneWindow, "Curve Tools", GUILayout.Width(x));
    }

    void SceneWindow(int id)
    {
        if (toolMode != ToolMode.Editing)
        {
            if (GUILayout.Button("Edit"))
                toolMode = ToolMode.Editing;
        }
        else
        {
            if (GUILayout.Button("End editing"))
            {
                toolMode = ToolMode.None;
                toolWindowRect.height = 0;
            }

            EditorGUILayout.Separator();

            DrawToolButtons();

            string handleTypeStr = "None";

            if (selectedPoints.Count > 0)
            {
                CurvePoint.HandleStyle handleStyle;
                handleStyle = curve[selectedPoints[0]].handleStyle;

                bool isMixed = false;
                for (int i = 1; i < selectedPoints.Count; i++)
                {
                    if (curve[selectedPoints[i]].handleStyle != handleStyle)
                        isMixed = true;
                }

                handleTypeStr = isMixed ? "Mixed" : handleStyle.ToString();
            }

            GUILayout.Label("Handle type: " + handleTypeStr + "; Change to:");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Equal")) ChangeHandleTypesTo(CurvePoint.HandleStyle.Equal);
            if (GUILayout.Button("Aligned")) ChangeHandleTypesTo(CurvePoint.HandleStyle.Aligned);
            if (GUILayout.Button("Broken")) ChangeHandleTypesTo(CurvePoint.HandleStyle.Broken);
            if (GUILayout.Button("None")) ChangeHandleTypesTo(CurvePoint.HandleStyle.None);
            GUILayout.EndHorizontal();

            GUILayout.Label("Selected: " + selectedPoints.Count);
        }
    }

    void ChangeHandleTypesTo(CurvePoint.HandleStyle style)
    {
        Undo.RegisterCompleteObjectUndo(curve, "Change Handle Type");
        for (int sp = 0; sp < selectedPoints.Count; sp++)
        {
            int i = selectedPoints[sp];
            curve[i].handleStyle = style;

            if (curve[i].handleStyle == CurvePoint.HandleStyle.None)
            {
                curve[i].handle1 = Vector3.zero;
                curve[i].handle2 = Vector3.zero;
            }

            curve[i].handleStyle = style;
        }
    }

    void HideIfLessThan(int i)
    {
        GUI.enabled = true;

        if (selectedPoints.Count < i)
            GUI.enabled = false;
    }

    void Unhide()
    {
        GUI.enabled = true;
    }

    void DrawToolButtons()
    {
        GUILayout.BeginHorizontal();

        HideIfLessThan(2);
        if (GUILayout.Button("Align"))
            AlignPoints();

        HideIfLessThan(1);
        if (GUILayout.Button("Level"))
            Level();

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();

        HideIfLessThan(2);
        if (GUILayout.Button("Subdivide"))
            Subdivide();

        HideIfLessThan(1);
        if (GUILayout.Button("Remove"))
            RemovePoints();

        GUILayout.EndHorizontal();

        if (selectedPoints.Count != 1) GUI.enabled = false;
        if (GUILayout.Button("Split"))
            Split();

        Unhide();

        lockDirection = GUILayout.Toggle(lockDirection, "Lock handle direction");
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

        //int newType = (int)((object)EditorGUILayout.EnumPopup("Handle Type", (CurvePoint.HandleStyle)handleStyleProp.enumValueIndex));

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(handleStyleProp);
        if (EditorGUI.EndChangeCheck())
        {
            int newType = handleStyleProp.enumValueIndex;
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

        // EQUAL bezier type
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
        // ALIGNED bezier type
        else if (handleStyleProp.enumValueIndex == 3)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(handle1Prop);
            if (EditorGUI.EndChangeCheck())
            {
                handle2Prop.vector3Value =
                    -handle1Prop.vector3Value.normalized * handle2Prop.vector3Value.magnitude;
            }

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(handle2Prop);
            if (EditorGUI.EndChangeCheck())
            {
                handle1Prop.vector3Value =
                    -handle2Prop.vector3Value.normalized * handle1Prop.vector3Value.magnitude;
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
        pointsProp.DeleteArrayElementAtIndex(index);

        //pointsProp.MoveArrayElement(index, curve.pointCount - 1);
        //pointsProp.arraySize--;

        curve.SetDirty();

        Undo.RegisterCompleteObjectUndo(target, "Remove Point");

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
                Undo.RegisterCompleteObjectUndo(point.curve, "Move Point");
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
                Undo.RegisterCompleteObjectUndo(point.curve, "Move Point");
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
                Vector3 newLocal = newGlobal1 - point.position;
                if (lockDirection)
                {
                    newLocal = Vector3.Project(newLocal, point.handle1.normalized);
                    newGlobal1 = point.position + newLocal;
                }

                Undo.RegisterCompleteObjectUndo(point.curve, "Move Handle");
                point.globalHandle1 = newGlobal1;

                if (point.handleStyle == CurvePoint.HandleStyle.Equal)
                    point.globalHandle2 = -(newGlobal1 - point.position) + point.position;
                else if (point.handleStyle == CurvePoint.HandleStyle.Aligned)
                {
                    Vector3 otherHandleTarget = -Vector3.Normalize(newGlobal1 - point.position);
                    point.globalHandle2 = otherHandleTarget * point.handle2.magnitude + point.position;
                }
            }

            Vector3 newGlobal2 = Handles.FreeMoveHandle(point.globalHandle2, Quaternion.identity, HandleUtility.GetHandleSize(point.globalHandle2) * 0.075f, Vector3.zero, Handles.CircleHandleCap);
            if (point.globalHandle2 != newGlobal2)
            {
                Vector3 newLocal = newGlobal2 - point.position;
                if (lockDirection)
                {
                    newLocal = Vector3.Project(newLocal, point.handle2.normalized);
                    newGlobal2 = point.position + newLocal;
                }

                Undo.RegisterCompleteObjectUndo(point.curve, "Move Handle");
                point.globalHandle2 = newGlobal2;

                if (point.handleStyle == CurvePoint.HandleStyle.Equal)
                    point.globalHandle1 = -(newGlobal2 - point.position) + point.position;
                else if (point.handleStyle == CurvePoint.HandleStyle.Aligned)
                {
                    Vector3 otherHandleTarget = -Vector3.Normalize(newGlobal2 - point.position);
                    point.globalHandle1 = otherHandleTarget * point.handle1.magnitude + point.position;
                }
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

    void CenterPivot()
    {
        if (curve.pointCount == 0)
            return;

        Undo.RegisterCompleteObjectUndo(target, "Center Pivot");

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

        //Undo.RegisterCompleteObjectUndo(curve, "Center Pivot");
        curveTransformSO.ApplyModifiedProperties();

        RegisterPointsChanged();
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

        RegisterPointsChanged();
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

            points[i].handle1 = -points[i].handle2.normalized * points[i].handle1.magnitude;
        }

        if (points.Count > 2)
        {
            for (int i = 1; i < points.Count - 1; i++)
            {
                points[i].position = Vector3.Project(points[i].position - median, normal) + median;
            }
        }

        Undo.RegisterCompleteObjectUndo(curve, "Align Points");
        RegisterPointsChanged();
    }

    void Subdivide()
    {
        selectedPoints.Sort();

        if (selectedPoints.Count < 2)
            return;

        for (int i = 0; i < selectedPoints.Count - 1; i++)
        {
            if (selectedPoints[i] + 1 != selectedPoints[i + 1])
            {
                //Debug.Log($"{selectedPoints[i]}, {selectedPoints[i + 1]}");
                Debug.LogWarning("Cannot subdivide non sequential points");
                return;
            }
        }

        Undo.RegisterCompleteObjectUndo(curve, "Subdivide");

        for (int i = selectedPoints.Count - 2; i >= 0; i--)
        {
            CurvePoint point1 = curve[selectedPoints[i]];
            CurvePoint point2 = curve[selectedPoints[i + 1]];

            BezierUtility.SplitBezier(0.5f,
                point1.position, point2.position,
                point1.globalHandle2, point2.globalHandle1,
                out _, out Vector3 leftEndPosition,
                out Vector3 leftStartTangent, out Vector3 leftEndTangent,
                out _, out _,
                out Vector3 rightStartTangent, out Vector3 rightEndTangent);

            CurvePoint newPoint = new CurvePoint(curve)
            {
                handleStyle = CurvePoint.HandleStyle.Aligned,

                position = leftEndPosition,
                globalHandle1 = leftEndTangent,
                globalHandle2 = rightStartTangent
            };

            int iN = selectedPoints[i] + 1;
            pointsProp.InsertArrayElementAtIndex(iN);

            // New point
            curve[iN].position = leftEndPosition;
            curve[iN].globalHandle1 = leftEndTangent;
            curve[iN].globalHandle2 = rightStartTangent;
            SetPointHandles(iN);

            int i0 = selectedPoints[i];
            curve[i0].globalHandle2 = leftStartTangent;
            SetPointHandles(i0);

            int i1 = selectedPoints[i] + 2;
            curve[i1].globalHandle1 = rightEndTangent;
            SetPointHandles(i1);

            RegisterPointsChanged();
        }

        selectedPoints.Clear();

        void SetPointHandles(int index)
        {
            var prop = pointsProp.GetArrayElementAtIndex(index);
            prop.FindPropertyRelative("handleStyle").enumValueIndex = (int)CurvePoint.HandleStyle.Aligned;
            prop.FindPropertyRelative("_position").vector3Value = curve[index].localPosition;
            prop.FindPropertyRelative("_handle1").vector3Value = curve[index].handle1;
            prop.FindPropertyRelative("_handle2").vector3Value = curve[index].handle2;
            serializedObject.ApplyModifiedProperties();
        }
    }

    void Level()
    {
        Vector3 median = Vector3.zero;
        for (int sp = 0; sp < selectedPoints.Count; sp++)
        {
            int i = selectedPoints[sp];
            Vector3 pos = curve[i].position;
            median += pos / selectedPoints.Count;
        }

        Undo.RegisterCompleteObjectUndo(curve, "Level Points");

        for (int sp = 0; sp < selectedPoints.Count; sp++)
        {
            int i = selectedPoints[sp];
            Vector3 pos = curve[i].position;

            pos.y = median.y;
            curve[i].handle1 = new Vector3(curve[i].handle1.x, 0, curve[i].handle1.z);
            curve[i].handle2 = new Vector3(curve[i].handle2.x, 0, curve[i].handle2.z);
            curve[i].position = pos;
        }

        curve.SetDirty();
        RegisterPointsChanged();
    }

    void Split()
    {
        if (selectedPoints.Count != 1)
            return;

        int splitIndex = selectedPoints[0];

        if (splitIndex == 0 || splitIndex == curve.pointCount - 1)
            return;

        GameObject newCurveGO = new GameObject(curve.name);

        newCurveGO.transform.parent = curve.transform.parent;
        newCurveGO.transform.position = curve.transform.position;
        newCurveGO.transform.rotation = curve.transform.rotation;
        newCurveGO.transform.localScale = curve.transform.localScale;

        Undo.RegisterCreatedObjectUndo(newCurveGO, "Split");

        BezierCurve newCurve = newCurveGO.AddComponent<BezierCurve>();

        for (int pi = splitIndex; pi < curve.pointCount; pi++)
        {
            newCurve.AddPoint(new CurvePoint(curve, curve[pi]));
        }

        Undo.RegisterCompleteObjectUndo(curve, "Split Curve");

        for (int pi = curve.pointCount - 1; pi > splitIndex; pi--)
        {
            pointsProp.DeleteArrayElementAtIndex(pi);
        }

        serializedObject.ApplyModifiedProperties();

        RegisterPointsChanged();
    }

    void Reverse()
    {
        Undo.RegisterCompleteObjectUndo(curve, "Reverse Curve");

        curve.Reverse();

        serializedObject.ApplyModifiedProperties();
    }

    bool GetMouseSceneHit(out RaycastHit hit)
    {
        Vector2 guiPosition = Event.current.mousePosition;
        Ray ray = HandleUtility.GUIPointToWorldRay(guiPosition);

        return Physics.Raycast(ray, out hit);
    }

    static BezierCurve CreateCurveObject(out Vector3 pos, Object parent)
    {
        GameObject curveObject = new GameObject("BezierCurve");
        pos = SceneView.lastActiveSceneView.pivot;
        GameObjectUtility.SetParentAndAlign(curveObject, parent as GameObject);
        Selection.activeObject = curveObject;

        if (!parent)
            curveObject.transform.position = pos;

        Undo.RegisterCreatedObjectUndo(curveObject, "Create Curve");

        BezierCurve curve = curveObject.AddComponent<BezierCurve>();

        return curve;
    }

    [MenuItem("GameObject/Bezier Curve/Empty", false, 6)]
    public static void CreateCurveEmpty(MenuCommand command)
    {
        CreateCurveObject(out _, command.context);
    }

    [MenuItem("GameObject/Bezier Curve/Circle", false, 6)]
    public static void CreateCurveCircle(MenuCommand command)
    {
        var curve = CreateCurveObject(out Vector3 pos, command.context);

        CurvePoint p1 = curve.AddPointAt(pos + Vector3.forward * 0.5f);
        p1.handleStyle = CurvePoint.HandleStyle.Equal;
        p1.handle1 = new Vector3(-0.28f, 0, 0);

        CurvePoint p2 = curve.AddPointAt(pos + Vector3.right * 0.5f);
        p2.handleStyle = CurvePoint.HandleStyle.Equal;
        p2.handle1 = new Vector3(0, 0, 0.28f);

        CurvePoint p3 = curve.AddPointAt(pos + -Vector3.forward * 0.5f);
        p3.handleStyle = CurvePoint.HandleStyle.Equal;
        p3.handle1 = new Vector3(0.28f, 0, 0);

        CurvePoint p4 = curve.AddPointAt(pos + -Vector3.right * 0.5f);
        p4.handleStyle = CurvePoint.HandleStyle.Equal;
        p4.handle1 = new Vector3(0, 0, -0.28f);

        curve.close = true;
    }

}
