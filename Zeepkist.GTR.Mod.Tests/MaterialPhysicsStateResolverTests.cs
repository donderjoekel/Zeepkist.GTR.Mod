using TNRD.Zeepkist.GTR.Ghosting.Recording;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests
{
    public class MaterialPhysicsStateResolverTests
    {
        [Theory]
        [InlineData(0, MaterialPhysicsState.Tarmac)]
        [InlineData(1, MaterialPhysicsState.Grass)]
        [InlineData(2, MaterialPhysicsState.Sand)]
        [InlineData(4, MaterialPhysicsState.Soap)]
        [InlineData(5, MaterialPhysicsState.Wood)]
        [InlineData(3, MaterialPhysicsState.Mud)]
        [InlineData(6, MaterialPhysicsState.Ice1)]
        [InlineData(7, MaterialPhysicsState.Ice2)]
        [InlineData(8, MaterialPhysicsState.Ice3)]
        public void FromLogicOutput_MapsGamePhysics(
            int logicOutput,
            MaterialPhysicsState expected)
        {
            Assert.Equal(expected, MaterialPhysicsStateResolver.FromLogicOutput(logicOutput));
        }

        [Theory]
        [InlineData(null)]
        [InlineData(-1)]
        [InlineData(9)]
        [InlineData(100)]
        public void FromLogicOutput_ReturnsNoneForMissingOrUnknownPhysics(int? logicOutput)
        {
            Assert.Equal(
                MaterialPhysicsState.None,
                MaterialPhysicsStateResolver.FromLogicOutput(logicOutput));
        }

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(false, true, false)]
        [InlineData(true, false, false)]
        [InlineData(true, true, true)]
        public void ShouldIncludeWheel_RequiresEnabledAndGrounded(
            bool enabled,
            bool grounded,
            bool expected)
        {
            Assert.Equal(expected, MaterialPhysicsStateResolver.ShouldIncludeWheel(enabled, grounded));
        }

        [Fact]
        public void GetEffectiveState_UsesSoapOverride()
        {
            Assert.Equal(
                MaterialPhysicsState.Soap,
                MaterialPhysicsStateResolver.GetEffectiveState(true, 5));
            Assert.Equal(
                MaterialPhysicsState.Wood,
                MaterialPhysicsStateResolver.GetEffectiveState(false, 5));
        }

        [Fact]
        public void GetEffectiveState_PreservesMissingPhysicsAndSoapOverride()
        {
            Assert.Equal(MaterialPhysicsState.None, MaterialPhysicsStateResolver.GetEffectiveState(false, null));
            Assert.Equal(MaterialPhysicsState.Soap, MaterialPhysicsStateResolver.GetEffectiveState(true, null));
        }

        [Theory]
        [InlineData("0 - Tarmac", 0, MaterialPhysicsState.Tarmac)]
        [InlineData("6 - Ice 0.05", 6, MaterialPhysicsState.Ice1)]
        [InlineData("renamed asset", 5, MaterialPhysicsState.Wood)]
        public void AssetName_DoesNotAffectPhysicsIdentity(string assetName, int logicOutput, MaterialPhysicsState expected)
        {
            var physics = new { name = assetName, logicOutput };
            Assert.Equal(expected, MaterialPhysicsStateResolver.GetEffectiveState(false, physics.logicOutput));
        }

        [Fact]
        public void Combine_PreservesMixedWheelSurfaces()
        {
            MaterialPhysicsState state = MaterialPhysicsStateResolver.Combine(
                MaterialPhysicsStateResolver.FromLogicOutput(5),
                MaterialPhysicsStateResolver.FromLogicOutput(3));
            state = MaterialPhysicsStateResolver.Combine(
                state,
                MaterialPhysicsStateResolver.FromLogicOutput(8));

            Assert.Equal(
                MaterialPhysicsState.Wood |
                MaterialPhysicsState.Mud |
                MaterialPhysicsState.Ice3,
                state);
        }

        [Fact]
        public void Combine_LeavesAirborneStateAsNone()
        {
            Assert.Equal(
                MaterialPhysicsState.None,
                MaterialPhysicsStateResolver.Combine(
                    MaterialPhysicsState.None,
                    MaterialPhysicsState.None));
        }
    }
}
