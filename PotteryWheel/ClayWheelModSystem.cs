using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SimplePotteryWheel;

public class ClayWheelModSystem : ModSystem
{
    public static ClayWheelModConfig config;
    private INetworkChannel channel;

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
                api.StoreModConfig(config, "potterywheel.json");
            }
        }
        catch (System.Exception)
        {
            api.Logger.Error("Could not load potterywheel config, using default values...");
            config = new ClayWheelModConfig();
        }

    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);
        api.Event.PlayerNowPlaying += OnPlayerNowPlaying;

        channel = api.Network.RegisterChannel("simplepotterywheel");
        channel.RegisterMessageType<ClayWheelModConfig>();
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);
        channel = api.Network.RegisterChannel("simplepotterywheel");
        channel.RegisterMessageType<ClayWheelModConfig>();
        (channel as IClientNetworkChannel).SetMessageHandler<ClayWheelModConfig>(OnConfigMessage);
    }


    private void OnPlayerNowPlaying(IServerPlayer byPlayer)
    {
        if (channel is IServerNetworkChannel schannel)
        {
            schannel.SendPacket(config, [byPlayer]);
        }
    }

    private void OnConfigMessage(ClayWheelModConfig modConfig)
    {
        config = modConfig;
    }

}