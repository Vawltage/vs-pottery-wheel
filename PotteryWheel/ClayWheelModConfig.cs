using ProtoBuf;

namespace SimplePotteryWheel;

[ProtoContract]
public class ClayWheelModConfig
{
    [ProtoMember(1)] public int voxelsPerUse = 1;
    [ProtoMember(2)] public float poweredMultiplier = 2.0f;
}