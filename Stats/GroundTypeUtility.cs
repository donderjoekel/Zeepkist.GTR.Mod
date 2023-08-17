using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Mod.Stats;

public class GroundTypeUtility : IComparer<RaycastHit>
{
    private static readonly ManualLogSource logger = BepInEx.Logging.Logger.CreateLogSource(nameof(GroundTypeUtility));

    private static readonly RaycastHit[] hits = new RaycastHit[8];
    private static readonly List<RaycastHit> buildingBlockHits = new();

    public static GroundTypeUtility Instance { get; } = new GroundTypeUtility();

    public bool TryGetGroundType(SetupCar setupCar, out GroundType? groundType)
    {
        groundType = null;

        if (setupCar.AreAllWheelsInAir())
            return false;

        Vector3 origin = setupCar.gameObject.transform.position;
        int size = Physics.RaycastNonAlloc(origin, -setupCar.gameObject.transform.up, hits, 2.5f);
        buildingBlockHits.Clear();

        for (int i = 0; i < size; i++)
        {
            RaycastHit hit = hits[i];
            if (!IsPartOfBuildingBlock(hit.transform))
                continue;

            buildingBlockHits.Add(hit);
        }

        buildingBlockHits.Sort(this);

        if (buildingBlockHits.Count == 0)
            return false;

        RaycastHit firstHit = buildingBlockHits.First();

        PhysicMaterial physicMaterial = GetPhysicMaterial(firstHit.transform.gameObject);
        if (physicMaterial == null)
            return false;

        switch (physicMaterial.name)
        {
            case "PHSX_Default":
                groundType = GroundType.Regular;
                break;
            case "PHSX_Grass":
                groundType = GroundType.Grass;
                break;
            case "PHSX_Ice":
                groundType = GroundType.Ice;
                break;
            default:
                return false;
        }

        return true;
    }

    private static bool IsPartOfBuildingBlock(Transform transform)
    {
        while (transform != null)
        {
            if (transform.CompareTag("BuildingBlock"))
                return true;
            transform = transform.parent;
        }

        return false;
    }

    private static PhysicMaterial GetPhysicMaterial(GameObject gameObject)
    {
        Collider collider = gameObject.GetComponent<Collider>();
        if (!collider)
            return null;

        PhysicMaterial material = collider.sharedMaterial;

        if (!material)
        {
            material = collider.material;
        }

        return material;
    }

    public int Compare(RaycastHit x, RaycastHit y)
    {
        return x.distance.CompareTo(y.distance);
    }
}
