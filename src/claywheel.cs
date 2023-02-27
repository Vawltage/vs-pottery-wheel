using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.GameContent.Mechanics;
using Vintagestory.API.MathTools;
using Vintagestory.API.Client;
using Vintagestory.API.Util;

namespace claywheel.src
{
    class ClayWheel : BlockMPBase
    {
        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;

            interactions = ObjectCacheUtil.GetOrCreate(api, "clayWheelInteractions", () =>
            {
                List<ItemStack> clayStackList = new List<ItemStack>();

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
                    new WorldInteraction()
                    {
                        ActionLangCode = "claywheel:blockhelp-claywheel-startclay",
                        HotKeyCode = "shift",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = clayStackList.ToArray(),
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            ClayWheelEntity becw = api.World.BlockAccessor.GetBlockEntity(bs.Position) as ClayWheelEntity;
                            if (becw != null && becw.SelectedRecipe != null)
                            {
                                return null;
                            }
                            else return clayStackList.ToArray();
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "claywheel:blockhelp-claywheel-addclay",
                        HotKeyCode = null,
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = clayStackList.ToArray(),
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            ClayWheelEntity becw = api.World.BlockAccessor.GetBlockEntity(bs.Position) as ClayWheelEntity;
                            List<ItemStack> stacks = new List<ItemStack>();

                            foreach (var val in wi.Itemstacks)
                            {
                                if (becw != null)
                                {
                                    if (becw.WorkItemStack != null && becw.WorkItemStack.Collectible.LastCodePart() == val.Collectible.LastCodePart())
                                    {
                                        stacks.Add(val);
                                    }
                                }
                            }
                            return stacks.ToArray();
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "claywheel:blockhelp-claywheel-retrieveclay",
                        HotKeyCode = "shift",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = clayStackList.ToArray(),
                        RequireFreeHand = true,
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            ClayWheelEntity becw = api.World.BlockAccessor.GetBlockEntity(bs.Position) as ClayWheelEntity;
                            if (becw != null && becw.SelectedRecipe != null)
                            {
                                return new ItemStack[] { becw.WorkItemStack };
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
                bool poop = tryConnect(world, byPlayer, blockSel.Position, BlockFacing.DOWN);
            }

            return ok;
        }

        //order of methods is: Start > Step(as long as being held) > Cancel > Stop
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as ClayWheelEntity;
            if (be != null)
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
                string clayOnWheelType = be.WorkItemStack != null ? be.WorkItemStack.Collectible.Code.ToString() : null;
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
            var be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as ClayWheelEntity;
            if (be != null)
            {
                ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
                if (slot.Itemstack == null) return false;
                string clayOnWheelType = be.WorkItemStack != null ? be.WorkItemStack.Collectible.Code.ToString() : null;
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
            var be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as ClayWheelEntity;
            if (be != null)
            {
                be.Spin(false);
                return true;
            }
            return base.OnBlockInteractCancel(secondsUsed, world, byPlayer, blockSel, cancelReason);
        }

        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as ClayWheelEntity;
            if (be != null)
            {
                be.Spin(false);
            }
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }

        public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {

        }

        public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            return face == BlockFacing.DOWN;
        }
    }
}