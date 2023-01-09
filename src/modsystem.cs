using Vintagestory.API.Common;

namespace claywheel.src
{
    public class ClayWheels : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterBlockClass("ClayWheel", typeof(ClayWheel));
            api.RegisterBlockEntityClass("ClayWheelEntity", typeof(ClayWheelEntity));
        }
    }
}