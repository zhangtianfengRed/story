using UnityEditor;
using UnityEngine;

public static class CityWalkPedestrianPatrolEditor
{
    [MenuItem(
        "GameObject/Story/City Walk/Add Waypoint Walking",
        false,
        30)]
    private static void AddPatrolToSelection()
    {
        foreach (GameObject selectedObject in Selection.gameObjects)
        {
            if (selectedObject.GetComponent<CityWalkPedestrianPatrol>() == null)
            {
                Undo.AddComponent<CityWalkPedestrianPatrol>(selectedObject);
            }
        }
    }

    [MenuItem(
        "GameObject/Story/City Walk/Add Waypoint Walking",
        true)]
    private static bool ValidateAddPatrolToSelection()
    {
        return Selection.gameObjects.Length > 0;
    }

    [MenuItem(
        "GameObject/Story/City Walk/Create 4-Point Route",
        false,
        31)]
    private static void CreateRouteForSelection()
    {
        foreach (GameObject selectedObject in Selection.gameObjects)
        {
            CreateRoute(selectedObject);
        }
    }

    [MenuItem(
        "GameObject/Story/City Walk/Create 4-Point Route",
        true)]
    private static bool ValidateCreateRouteForSelection()
    {
        return Selection.gameObjects.Length > 0;
    }

    private static void CreateRoute(GameObject pedestrian)
    {
        CityWalkPedestrianPatrol patrol =
            pedestrian.GetComponent<CityWalkPedestrianPatrol>();
        if (patrol == null)
        {
            patrol = Undo.AddComponent<CityWalkPedestrianPatrol>(pedestrian);
        }

        GameObject routeRoot = new GameObject($"{pedestrian.name}_Route");
        Undo.RegisterCreatedObjectUndo(routeRoot, "Create pedestrian route");
        routeRoot.transform.SetParent(pedestrian.transform.parent, true);

        Vector3 routeDirection = pedestrian.transform.forward;
        routeDirection.y = 0f;
        if (routeDirection.sqrMagnitude <= 0.000001f)
        {
            routeDirection = Vector3.right;
        }
        else
        {
            routeDirection.Normalize();
        }

        const int pointCount = 4;
        for (int i = 0; i < pointCount; i++)
        {
            GameObject point = new GameObject($"Point_{i + 1:00}");
            Undo.RegisterCreatedObjectUndo(point, "Create pedestrian route point");
            point.transform.SetParent(routeRoot.transform, true);
            point.transform.position = pedestrian.transform.position +
                routeDirection * (i * 3.5f);
        }

        Undo.RecordObject(patrol, "Assign pedestrian route");
        patrol.SetRouteRoot(routeRoot.transform);
        EditorUtility.SetDirty(patrol);
        Selection.activeGameObject = routeRoot;
    }
}
