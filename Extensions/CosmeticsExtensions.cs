using ZeepSDK.Cosmetics;

namespace TNRD.Zeepkist.GTR.Extensions;

public static class CosmeticsExtensions
{
    public static void FromPreV16(this CosmeticsV16 cosmetics, int soapboxId, int hatId, int colorId)
    {
        cosmetics.zeepkist = CosmeticsApi.GetSoapbox(soapboxId, false);
        cosmetics.hat = CosmeticsApi.GetHat(hatId, false);
        cosmetics.color_body = CosmeticsApi.GetColor(colorId, false);
        cosmetics.color_leftArm = CosmeticsApi.GetColor(colorId, false);
        cosmetics.color_rightArm = CosmeticsApi.GetColor(colorId, false);
        cosmetics.color_leftLeg = CosmeticsApi.GetColor(colorId, false);
        cosmetics.color_rightLeg = CosmeticsApi.GetColor(colorId, false);
    }
}
