using Vintagestory.API.Common;
using Vintagestory.GameContent.Mechanics;
using Vintagestory.API.MathTools;
using Vintagestory.API.Client;
using Vintagestory.API.Util;

namespace claywheel.src
{
    public class ClayWheelRenderer : IRenderer
    {
        internal bool ShouldRender;
        internal bool ShouldRotateManual;
        internal bool ShouldRotateAutomated;

        public BEBehaviorMPConsumer mechPowerPart;

        private ICoreClientAPI api;
        private BlockPos pos;
        private const float spinSpeed = 120;

        MeshRef baseMeshRef;
        public Matrixf BaseModelMat = new Matrixf();

        int texId;
        MeshRef workItemMeshRef;
        public Matrixf WorkItemModelMat = new Matrixf();

        public float AngleRad;

        public ClayWheelRenderer(ICoreClientAPI coreClientAPI, BlockPos pos, MeshData mesh)
        {
            this.api = coreClientAPI;
            this.pos = pos;
            baseMeshRef = coreClientAPI.Render.UploadMesh(mesh);
        }

        public double RenderOrder
        {
            get { return 0.5; }
        }

        public int RenderRange
        {
            get { return 24; }
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (workItemMeshRef != null)
            {
                RenderWorkItem();
            }

            //if the wheel is not spinning or the base mesh is null return
            if (baseMeshRef == null || !ShouldRender) return;

            IRenderAPI rpi = api.Render;
            Vec3d camPos = api.World.Player.Entity.CameraPos;

            rpi.GlDisableCullFace();
            rpi.GlToggleBlend(true);

            IStandardShaderProgram prog = rpi.PreparedStandardShader(pos.X, pos.Y, pos.Z);
            //prog.Tex2D = api.BlockTextureAtlas.AtlasTextureIds[0];
            prog.Tex2D = api.BlockTextureAtlas.AtlasTextures[0].TextureId;


            prog.ModelMatrix = BaseModelMat
                .Identity()
                .Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z)
                //center the model before rotating
                .Translate(0.5f, 0, 0.5f)
                .RotateY(AngleRad)
                .Translate(-0.5f, 0, -0.5f)
                .Values;

            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
            rpi.RenderMesh(baseMeshRef);
            prog.Stop();


            if (ShouldRotateManual)
            {
                AngleRad += deltaTime * spinSpeed * GameMath.DEG2RAD;
            }

            if (ShouldRotateAutomated && mechPowerPart != null)
            {
                AngleRad = mechPowerPart.AngleRad;
            }
        }

        private void RenderWorkItem()
        {
            IRenderAPI rpi = api.Render;
            IClientWorldAccessor worldAccess = api.World;
            Vec3d camPos = worldAccess.Player.Entity.CameraPos;

            rpi.GlDisableCullFace();
            IStandardShaderProgram prog = rpi.PreparedStandardShader(pos.X, pos.Y, pos.Z);
            rpi.BindTexture2d(texId);

            prog.ModelMatrix = WorkItemModelMat
                .Identity()
                .Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z)
                .Translate(0.5f, 1f, 0.5f)
                .RotateY(AngleRad)
                .Translate(-0.5f, 0, -0.5f)
                .Values;

            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

            rpi.RenderMesh(workItemMeshRef);

            prog.Stop();
        }

        public void RegenMesh(ItemStack workitem, bool[,,] Voxels)
        {
            if (workItemMeshRef != null)
            {
                workItemMeshRef.Dispose();
            }
            workItemMeshRef = null;

            if (workitem == null) return;

            //this.workItem = workitem;
            MeshData workItemMesh = new MeshData(24, 36, false);

            float subPixelPaddingx = api.BlockTextureAtlas.SubPixelPaddingX;
            float subPixelPaddingy = api.BlockTextureAtlas.SubPixelPaddingY;

            string texname = api.World.GetItem(new AssetLocation("clayworkitem-" + workitem.Collectible.LastCodePart())).Code.ToShortString();
            TextureAtlasPosition tpos = api.BlockTextureAtlas.GetPosition(api.World.GetBlock(new AssetLocation("clayform")), texname);
            MeshData singleVoxelMesh = CubeMeshUtil.GetCubeOnlyScaleXyz(1 / 32f, 1 / 32f, new Vec3f(1 / 32f, 1 / 32f, 1 / 32f));
            singleVoxelMesh.Rgba = new byte[6 * 4 * 4].Fill((byte)255);
            CubeMeshUtil.SetXyzFacesAndPacketNormals(singleVoxelMesh);

            texId = tpos.atlasTextureId;

            for (int i = 0; i < singleVoxelMesh.Uv.Length; i++)
            {
                if (i % 2 > 0)
                {
                    singleVoxelMesh.Uv[i] = tpos.y1 + singleVoxelMesh.Uv[i] * 2f / api.BlockTextureAtlas.Size.Height - subPixelPaddingy;
                }
                else
                {
                    singleVoxelMesh.Uv[i] = tpos.x1 + singleVoxelMesh.Uv[i] * 2f / api.BlockTextureAtlas.Size.Width - subPixelPaddingx;
                }
            }

            singleVoxelMesh.XyzFaces = (byte[])CubeMeshUtil.CubeFaceIndices.Clone();
            singleVoxelMesh.XyzFacesCount = 6;

            singleVoxelMesh.SeasonColorMapIds = new byte[6];
            singleVoxelMesh.ClimateColorMapIds = new byte[6];
            singleVoxelMesh.ColorMapIdsCount = 6;


            MeshData voxelMeshOffset = singleVoxelMesh.Clone();

            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        if (!Voxels[x, y, z]) continue;

                        float px = x / 16f;
                        float py = y / 16f;
                        float pz = z / 16f;

                        for (int i = 0; i < singleVoxelMesh.xyz.Length; i += 3)
                        {
                            voxelMeshOffset.xyz[i] = px + singleVoxelMesh.xyz[i];
                            voxelMeshOffset.xyz[i + 1] = py + singleVoxelMesh.xyz[i + 1];
                            voxelMeshOffset.xyz[i + 2] = pz + singleVoxelMesh.xyz[i + 2];
                        }

                        float offsetX = ((((x + 4 * y) % 16f / 16f)) * 32f) / api.BlockTextureAtlas.Size.Width;
                        float offsetY = (pz * 32f) / api.BlockTextureAtlas.Size.Height;

                        for (int i = 0; i < singleVoxelMesh.Uv.Length; i += 2)
                        {
                            voxelMeshOffset.Uv[i] = singleVoxelMesh.Uv[i] + offsetX;
                            voxelMeshOffset.Uv[i + 1] = singleVoxelMesh.Uv[i + 1] + offsetY;
                        }

                        workItemMesh.AddMeshData(voxelMeshOffset);
                    }
                }
            }

            workItemMeshRef = api.Render.UploadMesh(workItemMesh);
        }


        public void Dispose()
        {
            api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);

            if (baseMeshRef != null) baseMeshRef.Dispose();
            if (workItemMeshRef != null) workItemMeshRef.Dispose();
        }
    }
}