using Vintagestory.API.Common;

namespace SimplePotteryWheel;

public class ClayWheelModSystem : ModSystem
{
    public static ClayWheelModConfig config;
    public override void Start(ICoreAPI api)
    {
        base.Start(api);
        api.RegisterBlockClass("ClayWheel", typeof(ClayWheelBlock));
        api.RegisterBlockEntityClass("ClayWheelEntity", typeof(ClayWheelEntity));

        try
        {
            config = api.LoadModConfig<ClayWheelModConfig>("potterywheel.json");
            if (config == null)
            {
                config = new ClayWheelModConfig();
                api.StoreModConfig<ClayWheelModConfig>(config, "potterywheel.json");
            }
        }
        catch (System.Exception)
        {
            api.Logger.Error("Could not load potterywheel config, using default values...");
            config = new ClayWheelModConfig();
        }

    }
}

