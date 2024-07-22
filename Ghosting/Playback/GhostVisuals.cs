using UnityEngine;
using ZeepSDK.Utilities;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public class GhostVisuals : MonoBehaviour
{
    public SetupModelCar GhostModel { get; set; }
    public DisplayPlayerName NameDisplay { get; set; }
    public GameObject HornHolder { get; set; }
    public CosmeticsV16 Cosmetics { get; set; }

    private void Awake()
    {
        GhostModel = Instantiate(ComponentCache.Get<NetworkedGhostSpawner>().zeepkistGhostPrefab.ghostModel, transform);
        NameDisplay = Instantiate(
            ComponentCache.Get<NetworkedGhostSpawner>().zeepkistGhostPrefab.nameDisplay,
            ComponentCache.Get<NetworkedGhostSpawner>().zeepkistGhostPrefab.nameDisplay.transform.position,
            ComponentCache.Get<NetworkedGhostSpawner>().zeepkistGhostPrefab.nameDisplay.transform.rotation,
            transform);

        NameDisplay.enabled = false;

        foreach (Ghost_AnimateWheel wheel in GhostModel.GetComponentsInChildren<Ghost_AnimateWheel>())
        {
            wheel.enabled = false;
        }

        foreach (Ghost_AnimateWheel_v16 wheel in GhostModel.GetComponentsInChildren<Ghost_AnimateWheel_v16>())
        {
            wheel.enabled = false;
        }

        Cosmetics = new CosmeticsV16();
        HornHolder = GhostModel.transform.Find("Visible Horn").gameObject;
    }
}
