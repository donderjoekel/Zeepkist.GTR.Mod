using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ZeepSDK.Cosmetics;
using ZeepSDK.Utilities;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public class GhostVisuals : MonoBehaviour
{
    private static CosmeticsV16 _bulkCosmetics;

    public SetupModelCar GhostModel { get; set; }
    public DisplayPlayerName NameDisplay { get; set; }
    public GameObject HornHolder { get; set; }
    public CosmeticsV16 Cosmetics { get; set; }
    public List<Ghost_AnimateWheel_v16> Wheels { get; set; }

    public void Initialize(GhostVisualProfile profile)
    {
        GhostModel = Instantiate(ComponentCache.Get<NetworkedGhostSpawner>().zeepkistGhostPrefab.ghostModel, transform);
        if (profile == GhostVisualProfile.Full)
        {
            NameDisplay = Instantiate(
                ComponentCache.Get<NetworkedGhostSpawner>().zeepkistGhostPrefab.nameDisplay,
                ComponentCache.Get<NetworkedGhostSpawner>().zeepkistGhostPrefab.nameDisplay.transform.position,
                ComponentCache.Get<NetworkedGhostSpawner>().zeepkistGhostPrefab.nameDisplay.transform.rotation,
                transform);
            NameDisplay.enabled = false;
        }

        foreach (Ghost_AnimateWheel wheel in GhostModel.GetComponentsInChildren<Ghost_AnimateWheel>())
        {
            wheel.enabled = false;
        }

        Wheels = GhostModel.GetComponentsInChildren<Ghost_AnimateWheel_v16>().ToList();
        foreach (Ghost_AnimateWheel_v16 wheel in Wheels)
        {
            wheel.enabled = false;
        }

        Cosmetics = new CosmeticsV16();
        HornHolder = GhostModel.transform.Find("Visible Horn").gameObject;

        if (profile == GhostVisualProfile.Bulk)
            ConfigureBulkVisuals();
    }

    private void ConfigureBulkVisuals()
    {
        Cosmetics = ConfigureBulkModel(GhostModel);
        HornHolder.SetActive(false);
    }

    public static CosmeticsV16 ConfigureBulkModel(SetupModelCar model)
    {
        CosmeticsV16 cosmetics = GetBulkCosmetics();
        model.DoCarSetup(cosmetics, true, true, false);
        model.DisableParaglider();

        foreach (Ghost_AnimateWheel_v16 wheel in model.GetComponentsInChildren<Ghost_AnimateWheel_v16>())
        {
            wheel.enabled = false;
            wheel.offroadWheelModel.gameObject.SetActive(false);
            wheel.soapwheelModel.gameObject.SetActive(false);
            wheel.wheelModel.gameObject.SetActive(true);
        }

        GameObject hornHolder = model.transform.Find("Visible Horn")?.gameObject;
        if (hornHolder != null)
            hornHolder.SetActive(false);

        return cosmetics;
    }

    private static CosmeticsV16 GetBulkCosmetics()
    {
        if (_bulkCosmetics != null)
            return _bulkCosmetics;

        _bulkCosmetics = new CosmeticsV16
        {
            zeepkist = CosmeticsApi.GetAllZeepkists().First(),
            color_body = CosmeticsApi.GetAllColors().First()
        };
        _bulkCosmetics.color_leftArm = _bulkCosmetics.color_body;
        _bulkCosmetics.color_rightArm = _bulkCosmetics.color_body;
        _bulkCosmetics.color_leftLeg = _bulkCosmetics.color_body;
        _bulkCosmetics.color_rightLeg = _bulkCosmetics.color_body;
        return _bulkCosmetics;
    }
}
