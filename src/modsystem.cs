using Vintagestory.API.Common;

namespace claywheel.src
{
    public class ClayWheels : ModSystem
    {
        public static ModConfig config;
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterBlockClass("ClayWheel", typeof(ClayWheel));
            api.RegisterBlockEntityClass("ClayWheelEntity", typeof(ClayWheelEntity));

            try
            {
                config = api.LoadModConfig<ModConfig>("potterywheel.json");
                if (config == null)
                {
                    config = new ModConfig();
                    api.StoreModConfig<ModConfig>(config, "potterywheel.json");
                }
            }
            catch (System.Exception)
            {
                api.Logger.Error("Could not load potterywheel config, using default values...");
                config = new ModConfig();
            }

        }
    }
}