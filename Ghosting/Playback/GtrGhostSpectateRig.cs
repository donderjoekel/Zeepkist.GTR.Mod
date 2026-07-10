using UnityEngine;
using ZeepSDK.Utilities;
using Object = UnityEngine.Object;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public class GtrGhostSpectateRig : MonoBehaviour
{
    public SetupModelCar SoapboxRoot { get; private set; }
    public Transform SwaybarCameraPosition { get; private set; }
    public Transform BonnetCameraPosition { get; private set; }
    public GtrGhostCameraSwaybar Swaybar { get; private set; }

    public static GtrGhostSpectateRig TryCreate(GhostData ghostData)
    {
        if (ghostData == null ||
            ghostData.VisualProfile != GhostVisualProfile.Full ||
            ghostData.GameObject == null)
        {
            return null;
        }

        SetupModelCar soapbox = ghostData.GameObject.GetComponent<SetupModelCar>();
        if (soapbox == null)
            return null;

        NetworkedZeepkistGhost prefab = ComponentCache.Get<NetworkedGhostSpawner>().zeepkistGhostPrefab;
        if (prefab?.ghostSwaybar == null ||
            prefab.bonnetCameraPosition == null ||
            prefab.ghostModel == null)
        {
            return null;
        }

        var rigObject = new GameObject("GTR Spectate Rig");
        rigObject.transform.SetParent(soapbox.transform, false);
        rigObject.transform.localPosition = Vector3.zero;
        rigObject.transform.localRotation = Quaternion.identity;
        rigObject.transform.localScale = Vector3.one;

        var rig = rigObject.AddComponent<GtrGhostSpectateRig>();
        rig.Build(soapbox, prefab, rigObject.transform);
        return rig;
    }

    private void Build(SetupModelCar soapbox, NetworkedZeepkistGhost prefab, Transform rigRoot)
    {
        SoapboxRoot = soapbox;
        Transform soapboxTransform = soapbox.transform;
        Transform prefabModel = prefab.ghostModel.transform;

        GameObject swaybarObject = Object.Instantiate(prefab.ghostSwaybar.gameObject, rigRoot);
        swaybarObject.name = "GTR Ghost Swaybar";
        ApplyRelativePose(
            swaybarObject.transform,
            prefabModel,
            prefab.ghostSwaybar.transform,
            soapboxTransform);

        GhostCameraSwaybar vanillaSwaybar = swaybarObject.GetComponent<GhostCameraSwaybar>();
        Transform mainCameraPosition = vanillaSwaybar.mainCameraPosition;
        Object.Destroy(vanillaSwaybar);

        Swaybar = swaybarObject.AddComponent<GtrGhostCameraSwaybar>();
        Swaybar.SoapboxRoot = soapboxTransform;
        Swaybar.MainCameraPosition = mainCameraPosition;
        SwaybarCameraPosition = mainCameraPosition;

        var bonnetObject = new GameObject("GTR Bonnet Camera");
        bonnetObject.transform.SetParent(rigRoot, false);
        ApplyRelativePose(
            bonnetObject.transform,
            prefabModel,
            prefab.bonnetCameraPosition,
            soapboxTransform);
        BonnetCameraPosition = bonnetObject.transform;
    }

    private static void ApplyRelativePose(
        Transform target,
        Transform templateRoot,
        Transform templateTransform,
        Transform destinationRoot)
    {
        target.localPosition = templateRoot.InverseTransformPoint(templateTransform.position);
        target.localRotation = Quaternion.Inverse(templateRoot.rotation) * templateTransform.rotation;
        target.localScale = templateTransform.localScale;
    }
}
