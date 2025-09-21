using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;
using Vintagestory.GameContent.Mechanics;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Datastructures;

namespace SimplePotteryWheel
{
    public class ClayWheelEntity : BlockEntity
    {
        ItemStack workItemStack;
        int selectedRecipeId = -1;
        bool automated = false;
        bool canAddClay = true;
        float poweredMult
        {
            get
            {
                if (automated)
                {
                    return ClayWheelModSystem.config.poweredMultiplier;
                }
                else
                {
                    return 1.0f;
                }
            }
        }
        int clayAddedPerUse
        {
            get { return (int)(ClayWheelModSystem.config.voxelsPerUse * poweredMult); }
        }
        public int AvailableVoxels;
        public bool[,,] Voxels = new bool[16, 16, 16];

        GuiDialog dlg;
        BEBehaviorMPConsumer mpc;
        ClayWheelRenderer renderer;
        public bool playerSpinning = false;

        private AssetLocation shapeAL = AssetLocation.Create("shapes/block/claywheel.json", "claywheel");
        private AssetLocation clayFormSound = AssetLocation.Create("sounds/player/clayform.ogg");
        ILoadedSound spinSound;

        public ClayFormingRecipe SelectedRecipe
        {
            get { return Api != null ? Api.GetClayformingRecipes().FirstOrDefault(r => r.RecipeId == selectedRecipeId) : null; }
        }

        public ItemStack WorkItemStack
        {
            get { return workItemStack; }
        }

        MeshData baseMesh
        {
            get
            {
                object value;
                Api.ObjectCache.TryGetValue("claywheelmesh", out value);
                return (MeshData)value;
            }
            set { Api.ObjectCache["claywheelmesh"] = value; }
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            RegisterGameTickListener(Every100ms, 100);

            if (spinSound == null && api.Side == EnumAppSide.Client)
            {
                spinSound = ((IClientWorldAccessor)api.World).LoadSound(new SoundParams()
                {
                    Location = new AssetLocation("claywheel:sounds/wood-wheel.ogg"),
                    ShouldLoop = true,
                    Position = Pos.ToVec3f().Add(0.5f, 0.5f, 0.5f),
                    DisposeOnFinish = false,
                    Volume = 0.4f
                });
            }

            //when the block is reloaded resolve the current work item
            if (workItemStack != null)
            {
                workItemStack.ResolveBlockOrItem(api.World);
            }

            if (api.World.Side == EnumAppSide.Client)
            {
                renderer = new ClayWheelRenderer(api as ICoreClientAPI, Pos, GenMesh());
                renderer.mechPowerPart = this.mpc;
                if (automated)
                {
                    renderer.ShouldRender = true;
                    renderer.ShouldRotateAutomated = true;
                }

                (api as ICoreClientAPI).Event.RegisterRenderer(renderer, EnumRenderStage.Opaque, "claywheel");

                if (baseMesh == null)
                {
                    baseMesh = GenMesh();
                }

                RegenWorkItemMesh();
            }
        }

        public override void CreateBehaviors(Block block, IWorldAccessor worldForResolve)
        {
            base.CreateBehaviors(block, worldForResolve);

            mpc = GetBehavior<BEBehaviorMPConsumer>();
            if (mpc != null)
            {
                mpc.OnConnected = () =>
                {
                    automated = true;

                    if (renderer != null)
                    {
                        renderer.ShouldRender = true;
                        renderer.ShouldRotateAutomated = true;
                    }
                };

                mpc.OnDisconnected = () =>
                {
                    automated = false;

                    if (renderer != null)
                    {
                        renderer.ShouldRender = false;
                        renderer.ShouldRotateAutomated = false;
                    }
                };
            }
        }

        private void Every100ms(float dt)
        {
            if (playerSpinning)
            {
                if (spinSound != null && !spinSound.IsPlaying)
                {
                    spinSound.Start();
                }
            }
            else if (automated && mpc.TrueSpeed > 0.1f)
            {
                if (spinSound != null && !spinSound.IsPlaying)
                {
                    spinSound.Start();
                }
            }
            else
            {
                if (spinSound != null && spinSound.IsPlaying)
                {
                    spinSound.Stop();
                }
            }
        }

        public void PutClay(ItemSlot slot, IPlayer byPlayer)
        {
            if (!canAddClay) return;

            //if we don't have a recipe bring up the recipe selector otherwise add clay
            if (SelectedRecipe == null)
            {
                if (Api.World is IClientWorldAccessor)
                {
                    OpenDialog(Api.World as IClientWorldAccessor, Pos, slot.Itemstack);
                }

                workItemStack = slot.Itemstack.Clone();
                workItemStack.StackSize = 1;

                //normal clay forming gives 89 voxels total when first placed
                //we place 9 then give 80
                AvailableVoxels += 80;

                slot.TakeOut(1);
                slot.MarkDirty();
            }
            else
            {
                if (Api.Side == EnumAppSide.Client)
                {
                    SendAddClayPacket(byPlayer, GetPositionsToAdd());
                    canAddClay = false;
                }

                if (AvailableVoxels <= 0)
                {
                    AvailableVoxels += 25;

                    slot.TakeOut(1);
                    slot.MarkDirty();

                    workItemStack.StackSize += 1;
                }
            }


            RegenWorkItemMesh();
            MarkDirty();
        }

        private void AddClay(Vec3i[] positions)
        {
            foreach (Vec3i position in positions)
            {
                if (position != null)
                {
                    if (!Voxels[position.X, position.Y, position.Z])
                    {
                        Voxels[position.X, position.Y, position.Z] = true;
                        AvailableVoxels--;
                    }
                }
            }
        }

        //we only add 1 voxel at a time when unpowered and 2 when powered
        private Vec3i[] GetPositionsToAdd()
        {
            Vec3i[] positions = new Vec3i[clayAddedPerUse];
            int count = 0;
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        if (!Voxels[x, y, z] && SelectedRecipe.Voxels[x, y, z])
                        {
                            positions[count] = new Vec3i(x, y, z);
                            count++;
                        }

                        if (count == clayAddedPerUse)
                        {
                            return positions;
                        }
                    }
                }
            }
            return positions;
        }

        private void CheckIfFinished(IPlayer byPlayer)
        {
            if (SelectedRecipe == null) return;

            bool done = true;
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        //if we find an unfilled voxel that need to be filled we aren't done
                        if (!Voxels[x, y, z] && SelectedRecipe.Voxels[x, y, z])
                        {
                            done = false;
                        }
                    }
                }
            }

            if (done)
            {
                ItemStack outstack = SelectedRecipe.Output.ResolvedItemstack.Clone();
                ResetClayWheel();

                //not sure if we need to set max tries but that is how the base clay forming does it.
                //also just good sanity check
                int tries = 500;
                while (outstack.StackSize > 0 && tries-- > 0)
                {
                    ItemStack dropStack = outstack.Clone();
                    dropStack.StackSize = Math.Min(outstack.StackSize, outstack.Collectible.MaxStackSize);
                    outstack.StackSize -= dropStack.StackSize;

                    if (byPlayer.InventoryManager.TryGiveItemstack(dropStack))
                    {
                        Api.World.PlaySoundAt(new AssetLocation("sounds/player/collect"), byPlayer);
                    }
                    else
                    {
                        Api.World.SpawnItemEntity(dropStack, Pos.ToVec3d().Add(0.5, 1.0, 0.5));
                    }
                }

                if (tries <= 1)
                {
                    Api.World.Logger.Error("Tried to drop finished clay forming item but failed after 500 times?! Gave up doing so. Out stack was " + outstack);
                }
            }
        }

        public void RetreiveClay(IPlayer byPlayer)
        {
            ItemStack outstack = workItemStack.Clone();
            ResetClayWheel();

            int tries = 500;
            while (outstack.StackSize > 0 && tries-- > 0)
            {
                ItemStack dropStack = outstack.Clone();
                dropStack.StackSize = Math.Min(outstack.StackSize, outstack.Collectible.MaxStackSize);
                outstack.StackSize -= dropStack.StackSize;

                if (byPlayer.InventoryManager.TryGiveItemstack(dropStack))
                {
                    Api.World.PlaySoundAt(new AssetLocation("sounds/player/collect"), byPlayer);
                }
                else
                {
                    Api.World.SpawnItemEntity(dropStack, Pos.ToVec3d().Add(0.5, 1.0, 0.5));
                }
            }

            if (tries <= 1)
            {
                Api.World.Logger.Error("Tried to return clay but failed after 500 times?! Gave up doing so. Out stack was " + outstack);
            }

            RegenWorkItemMesh();
            MarkDirty();
        }

        //we place the first 9 voxels when the work item is first made
        public void CreateInitialWorkItem()
        {
            Voxels = new bool[16, 16, 16];

            int quantity = 9;
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        if (!Voxels[x, y, z] && SelectedRecipe.Voxels[x, y, z])
                        {
                            Voxels[x, y, z] = true;
                            quantity--;
                        }

                        if (quantity == 0)
                        {
                            return;
                        }
                    }
                }
            }
        }

        private void ResetClayWheel()
        {
            workItemStack = null;
            Voxels = new bool[16, 16, 16];
            AvailableVoxels = 0;
            selectedRecipeId = -1;

            if (renderer != null)
            {
                renderer.AngleRad = 0f;
            }

            MarkDirty();
        }

        void RegenWorkItemMesh()
        {
            if (renderer != null)
            {
                renderer.RegenMesh(workItemStack, Voxels);
            }
        }

        //this is for the base mesh ie the clay wheel itself
        internal MeshData GenMesh()
        {
            Block block = Api.World.BlockAccessor.GetBlock(Pos);
            if (block.BlockId == 0) return null;

            MeshData mesh;
            ITesselatorAPI mesher = ((ICoreClientAPI)Api).Tesselator;

            Shape meshShape = Shape.TryGet(Api, shapeAL);
            mesher.TesselateShape(block, meshShape, out mesh);

            return mesh;
        }

        //currently spinning and adding clay are seperate so we can spin without doing any work
        public void Spin(bool isSpinning)
        {
            if (automated) return;

            playerSpinning = isSpinning;

            if (renderer != null && SelectedRecipe != null)
            {
                renderer.ShouldRotateManual = playerSpinning;
            }

            Api.World.BlockAccessor.MarkBlockDirty(Pos, OnRestesselated);
        }

        private void OnRestesselated()
        {
            if (renderer == null) return;

            renderer.ShouldRender = playerSpinning || automated;
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            if (Block == null) return false;

            if (!playerSpinning && !automated)
            {
                mesher.AddMeshData(
                    this.baseMesh.Clone()
                    .Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, renderer.AngleRad, 0)
                );
            }
            return true;
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            base.OnBlockBroken(byPlayer);

            //drop clay if broken
            if (workItemStack != null)
            {
                Api.World.SpawnItemEntity(workItemStack, Pos.ToVec3d().Add(0.5, 1.0, 0.5));
            }
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (spinSound != null)
            {
                spinSound.Stop();
                spinSound.Dispose();
            }

            if (renderer != null)
            {
                renderer.Dispose();
            }
            renderer = null;
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            if (renderer != null)
            {
                renderer.Dispose();
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            bool modified = deserializeVoxels(tree.GetBytes("voxels"));
            workItemStack = tree.GetItemstack("workItemStack");
            AvailableVoxels = tree.GetInt("availableVoxels");
            selectedRecipeId = tree.GetInt("selectedRecipeId", -1);

            if (Api != null && workItemStack != null)
            {
                workItemStack.ResolveBlockOrItem(Api.World);
            }

            if (modified)
            {
                RegenWorkItemMesh();
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetBytes("voxels", serializeVoxels());
            tree.SetItemstack("workItemStack", workItemStack);
            tree.SetInt("availableVoxels", AvailableVoxels);
            tree.SetInt("selectedRecipeId", selectedRecipeId);
        }

        byte[] serializeVoxels()
        {
            byte[] data = new byte[16 * 16 * 16 / 8];
            int pos = 0;

            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        int bitpos = pos % 8;
                        data[pos / 8] |= (byte)((Voxels[x, y, z] ? 1 : 0) << bitpos);
                        pos++;
                    }
                }
            }

            return data;
        }

        bool deserializeVoxels(byte[] data)
        {
            if (data == null || data.Length < 16 * 16 * 16 / 8)
            {
                Voxels = new bool[16, 16, 16];
                return true;
            }

            if (Voxels == null) Voxels = new bool[16, 16, 16];


            int pos = 0;
            bool modified = false;

            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        int bitpos = pos % 8;
                        bool voxel = (data[pos / 8] & (1 << bitpos)) > 0;
                        modified |= Voxels[x, y, z] != voxel;

                        Voxels[x, y, z] = voxel;
                        pos++;
                    }
                }
            }

            return modified;
        }

        public void SendAddClayPacket(IPlayer byPlayer, Vec3i[] voxels)
        {
            byte[] data;

            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(ms);
                foreach (Vec3i voxel in voxels)
                {
                    if (voxel != null)
                    {
                        writer.Write(voxel.X);
                        writer.Write(voxel.Y);
                        writer.Write(voxel.Z);
                    }
                }
                data = ms.ToArray();
            }

            ((ICoreClientAPI)Api).Network.SendBlockEntityPacket(
                Pos,
                (int)EnumClayWheelPacket.AddClay,
                data
            );
        }

        public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
        {
            if (packetid == (int)EnumClayWheelPacket.CancelSelect)
            {
                if (workItemStack != null)
                {
                    Api.World.SpawnItemEntity(workItemStack, Pos.ToVec3d().Add(0.5, 1.0, 0.5));
                }
                ResetClayWheel();
            }

            if (packetid == (int)EnumClayWheelPacket.SelectRecipe)
            {
                int recipeid = SerializerUtil.Deserialize<int>(data);
                ClayFormingRecipe recipe = Api.GetClayformingRecipes().FirstOrDefault(r => r.RecipeId == recipeid);

                if (recipe == null)
                {
                    Api.World.Logger.Error("Client tried to selected clayforming recipe with id {0}, but no such recipe exists!");
                    return;
                }

                selectedRecipeId = recipe.RecipeId;
                CreateInitialWorkItem();
                RegenWorkItemMesh();
                Api.World.PlaySoundAt(clayFormSound, Pos.X, Pos.Y, Pos.Z, null, true, 8);
                // Tell server to save this chunk to disk again
                MarkDirty();
                Api.World.BlockAccessor.GetChunkAtBlockPos(Pos).MarkModified();
            }

            if (packetid == (int)EnumClayWheelPacket.AddClay)
            {
                Vec3i[] voxels = new Vec3i[clayAddedPerUse];
                using (MemoryStream ms = new MemoryStream(data))
                {
                    //each vec3i should be 12 bytes each
                    int count = (int)ms.Length / 12;
                    BinaryReader reader = new BinaryReader(ms);
                    for (int i = 0; i < count; i++)
                    {
                        voxels.SetValue(new Vec3i(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()), i);
                    }
                }


                AddClay(voxels);
                //too spammy will look for a better solution later
                //Api.World.PlaySoundAt(clayFormSound, Pos.X, Pos.Y, Pos.Z, null, true, 8);
                CheckIfFinished(player);
                RegenWorkItemMesh();

                ((ICoreServerAPI)Api).Network.SendBlockEntityPacket((IServerPlayer)player, Pos, (int)EnumClayWheelPacket.ClayAdded);
            }
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            base.OnReceivedServerPacket(packetid, data);

            if (packetid == (int)EnumClayWheelPacket.ClayAdded)
            {
                canAddClay = true;
            }
        }

        public void OpenDialog(IClientWorldAccessor world, BlockPos pos, ItemStack ingredient)
        {
            if (ingredient.Collectible is ItemWorkItem)
            {
                ingredient = new ItemStack(world.GetItem(new AssetLocation("clay-" + ingredient.Collectible.LastCodePart())));
            }

            List<ClayFormingRecipe> recipes = Api.GetClayformingRecipes()
                .Where(r => r.Ingredient.SatisfiesAsIngredient(ingredient))
                .OrderBy(r => r.Output.ResolvedItemstack.Collectible.Code) // Cannot sort by name, thats language dependent!
                .ToList();
            ;
            List<ItemStack> stacks = recipes
                .Select(r => r.Output.ResolvedItemstack)
                .ToList()
            ;

            ICoreClientAPI capi = Api as ICoreClientAPI;

            dlg = new GuiDialogBlockEntityRecipeSelector(
                Lang.Get("Select recipe"),
                stacks.ToArray(),
                (selectedIndex) =>
                {
                    capi.Logger.VerboseDebug("Select clay from recipe {0}, have {1} recipes.", selectedIndex, recipes.Count);

                    selectedRecipeId = recipes[selectedIndex].RecipeId;
                    capi.Network.SendBlockEntityPacket(pos, (int)EnumClayWheelPacket.SelectRecipe, SerializerUtil.Serialize(recipes[selectedIndex].RecipeId));
                },
                () =>
                {
                    capi.Network.SendBlockEntityPacket(pos, (int)EnumClayWheelPacket.CancelSelect);
                },
                pos,
                Api as ICoreClientAPI
            );

            dlg.OnClosed += dlg.Dispose;
            dlg.TryOpen();
        }

        //add some extra info when looking at the block
        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            if (workItemStack == null || SelectedRecipe == null) return;

            dsc.AppendLine(Lang.Get("Output: {0}", SelectedRecipe.Output.ResolvedItemstack.GetName()));
            dsc.AppendLine(Lang.Get("Available Voxels: {0}", AvailableVoxels));
        }

        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            if (workItemStack != null)
            {
                workItemStack.Collectible.OnStoreCollectibleMappings(Api.World, new DummySlot(workItemStack), blockIdMapping, itemIdMapping);
            }
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
        {
            if (workItemStack != null && workItemStack.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve) == false)
            {
                workItemStack = null;
            }
        }
    }

    enum EnumClayWheelPacket
    {
        OpenDialog = 7000,
        SelectRecipe = 7001,
        AddClay = 7002,
        CancelSelect = 7003,
        ClayAdded = 7004
    }
}