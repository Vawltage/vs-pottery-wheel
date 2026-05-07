using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.GameContent.Mechanics;
using Vintagestory.API.MathTools;
using Vintagestory.API.Client;
using Vintagestory.API.Util;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;

namespace SimplePotteryWheel
{
    class ClayWheelBlock : BlockMPBase
    {
        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;

            interactions = ObjectCacheUtil.GetOrCreate(api, "clayWheelInteractions", () =>
            {
                List<ItemStack> clayStackList = [];

                foreach (CollectibleObject obj in api.World.Collectibles)
                {
                    string firstCodePart = obj.FirstCodePart();

                    if (firstCodePart == "clay")
                    {
                        List<ItemStack> stacks = obj.GetHandBookStacks(capi);
                        if (stacks != null) clayStackList.AddRange(stacks);
                    }
                }

                return new WorldInteraction[]
                {
                    new()
                    {
                        ActionLangCode = "claywheel:blockhelp-claywheel-startclay",
                        HotKeyCode = "shift",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = [.. clayStackList],
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            if (api.World.BlockAccessor.GetBlockEntity(bs.Position) is ClayWheelEntity becw && becw.SelectedRecipe != null)
                            {
                                return null;
                            }
                            else return [.. clayStackList];
                        }
                    },
                    new()
                    {
                        ActionLangCode = "claywheel:blockhelp-claywheel-addclay",
                        HotKeyCode = null,
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = [.. clayStackList],
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            List<ItemStack> stacks = [];

                            foreach (var val in wi.Itemstacks)
                            {
                                if (
                                    api.World.BlockAccessor.GetBlockEntity(bs.Position) is ClayWheelEntity becw
                                    && becw.WorkItemStack?.Collectible.LastCodePart() == val.Collectible.LastCodePart()
                                )
                                {
                                    stacks.Add(val);
                                }
                            }
                            return [.. stacks];
                        }
                    },
                    new()
                    {
                        ActionLangCode = "claywheel:blockhelp-claywheel-retrieveclay",
                        HotKeyCode = "shift",
                        MouseButton = EnumMouseButton.Right,
                        RequireFreeHand = true,
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            if (api.World.BlockAccessor.GetBlockEntity(bs.Position) is ClayWheelEntity becw && becw.SelectedRecipe != null)
                            {
                                return [becw.WorkItemStack];
                            }
                            else return null;
                        }
                    }
                };
            }
            );

        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            bool ok = base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);

            if (ok)
            {
                tryConnect(world, byPlayer, blockSel.Position, BlockFacing.DOWN);
            }

            return ok;
        }

        //order of methods is: Start > Step(as long as being held) > Cancel > Stop
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is ClayWheelEntity be && blockSel.SelectionBoxIndex == 1)
            {
                ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
                if (slot.Itemstack == null)
                {
                    if (be.WorkItemStack != null && byPlayer.Entity.Controls.ShiftKey)
                    {
                        //If empty hand and shift+rmb return clay
                        be.RetreiveClay(byPlayer);
                        return true;
                    }
                    else return false;
                }
                string clayOnWheelType = be.WorkItemStack?.Collectible.Code.ToString();
                CollectibleObject heldItem = slot.Itemstack.Collectible;
                if (byPlayer.Entity.Controls.ShiftKey && be.SelectedRecipe == null && heldItem.Code.FirstCodePart() == "clay")
                {
                    //if shift+rmb and clay in hand and no current recipe pull up recipe selector and don't spin
                    be.PutClay(slot, byPlayer);
                    return true;
                }
                else if (be.SelectedRecipe != null && heldItem.Code.ToString() == clayOnWheelType)
                {
                    //if rmb with correct clay only spin on start and add on step
                    be.Spin(true);
                    return true;
                }
                else return false;
            }
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is ClayWheelEntity be && blockSel.SelectionBoxIndex == 1)
            {
                ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
                if (slot.Itemstack == null) return false;
                string clayOnWheelType = be.WorkItemStack?.Collectible.Code.ToString();
                CollectibleObject heldItem = slot.Itemstack.Collectible;
                if (be.SelectedRecipe != null && heldItem.Code.ToString() == clayOnWheelType)
                {
                    //add clay and spin when holding clay while holding rmb
                    be.PutClay(slot, byPlayer);
                    be.Spin(true);
                    return true;
                }
                else return false;
            }
            return base.OnBlockInteractStep(secondsUsed, world, byPlayer, blockSel);
        }

        public override bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
        {
            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is ClayWheelEntity be)
            {
                be.Spin(false);
                return true;
            }
            return base.OnBlockInteractCancel(secondsUsed, world, byPlayer, blockSel, cancelReason);
        }

        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is ClayWheelEntity be)
            {
                be.Spin(false);
            }
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            if (selection.SelectionBoxIndex == 1)
            {
                return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
            }

            return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);
        }

        public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {

        }

        public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face, BlockMPBase forBlock)
        {
            return face == BlockFacing.DOWN;
        }

        public override void OnEntityCollide(IWorldAccessor world, Entity entity, BlockPos pos, BlockFacing facing, Vec3d collideSpeed, bool isImpact)
        {
            base.OnEntityCollide(world, entity, pos, facing, collideSpeed, isImpact);

            if (facing == BlockFacing.UP && entity.Pos.Y > pos.Y + 0.75)
            {
                if (entity.World.Side == EnumAppSide.Server)
                {
                    float frameTime = GlobalConstants.PhysicsFrameTime;
                    var mpc = GetBEBehavior<BEBehaviorMPConsumer>(pos);
                    if (mpc != null)
                    {
                        entity.Pos.Yaw += frameTime * mpc.TrueSpeed * 2.5f * (mpc.IsRotationReversed() ? -1 : 1);
                    }
                }
                else
                {
                    float frameTime = GlobalConstants.PhysicsFrameTime;
                    var mpc = GetBEBehavior<BEBehaviorMPConsumer>(pos);
                    var capi = api as ICoreClientAPI;
                    if (capi.World.Player.Entity.EntityId == entity.EntityId)
                    {
                        var sign = mpc.IsRotationReversed() ? -1 : 1;
                        if (capi.World.Player.CameraMode != EnumCameraMode.Overhead)
                        {
                            capi.Input.MouseYaw += frameTime * mpc.TrueSpeed * 2.5f * sign;
                        }
                        capi.World.Player.Entity.BodyYaw += frameTime * mpc.TrueSpeed * 2.5f * sign;
                        capi.World.Player.Entity.WalkYaw += frameTime * mpc.TrueSpeed * 2.5f * sign;
                        capi.World.Player.Entity.Pos.Yaw += frameTime * mpc.TrueSpeed * 2.5f * sign;
                    }
                }
            }
        }

    }
}