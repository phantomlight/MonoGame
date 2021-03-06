using System;
using System.Diagnostics;
using System.Collections.Generic;

#if OPENGL
#if MONOMAC
using MonoMac.OpenGL;
#elif WINDOWS || LINUX
using OpenTK.Graphics.OpenGL;
#elif GLES
using OpenTK.Graphics.ES20;
using EnableCap = OpenTK.Graphics.ES20.All;
using FrontFaceDirection = OpenTK.Graphics.ES20.All;
using CullFaceMode = OpenTK.Graphics.ES20.All;
#endif
#elif PSM
using Sce.PlayStation.Core.Graphics;
#endif

namespace Microsoft.Xna.Framework.Graphics
{
	public class RasterizerState : GraphicsResource
	{
#if DIRECTX 
        private SharpDX.Direct3D11.RasterizerState _state;
#endif

        // TODO: We should be asserting if the state has
        // been changed after it has been bound to the device!

        public CullMode CullMode { get; set; }
        public float DepthBias { get; set; }
        public FillMode FillMode { get; set; }
        public bool MultiSampleAntiAlias { get; set; }
        public bool ScissorTestEnable { get; set; }
        public float SlopeScaleDepthBias { get; set; }

		private static readonly Utilities.ObjectFactoryWithReset<RasterizerState> _cullClockwise;
        private static readonly Utilities.ObjectFactoryWithReset<RasterizerState> _cullCounterClockwise;
        private static readonly Utilities.ObjectFactoryWithReset<RasterizerState> _cullNone;

        public static RasterizerState CullClockwise { get { return _cullClockwise.Value; } }
        public static RasterizerState CullCounterClockwise { get { return _cullCounterClockwise.Value; } }
        public static RasterizerState CullNone { get { return _cullNone.Value; } }
        
        public RasterizerState()
		{
			CullMode = CullMode.CullCounterClockwiseFace;
			FillMode = FillMode.Solid;
			DepthBias = 0;
			MultiSampleAntiAlias = true;
			ScissorTestEnable = false;
			SlopeScaleDepthBias = 0;
		}

		static RasterizerState ()
		{
			_cullClockwise = new Utilities.ObjectFactoryWithReset<RasterizerState>(() => new RasterizerState
            {
				CullMode = CullMode.CullClockwiseFace
			});

			_cullCounterClockwise = new Utilities.ObjectFactoryWithReset<RasterizerState>(() => new RasterizerState
            {
				CullMode = CullMode.CullCounterClockwiseFace
			});

			_cullNone = new Utilities.ObjectFactoryWithReset<RasterizerState>(() => new RasterizerState
            {
				CullMode = CullMode.None
			});
		}

#if OPENGL

        internal void ApplyState(GraphicsDevice device)
        {
        	// When rendering offscreen the faces change order.
            var offscreen = device.GetRenderTargets().Length > 0;

            if (CullMode == CullMode.None)
            {
                GL.Disable(EnableCap.CullFace);
                GraphicsExtensions.CheckGLError();
            }
            else
            {
                GL.Enable(EnableCap.CullFace);
                GraphicsExtensions.CheckGLError();
                GL.CullFace(CullFaceMode.Back);
                GraphicsExtensions.CheckGLError();

                if (CullMode == CullMode.CullClockwiseFace)
                {
                    if (offscreen)
                        GL.FrontFace(FrontFaceDirection.Cw);
                    else
                        GL.FrontFace(FrontFaceDirection.Ccw);
                    GraphicsExtensions.CheckGLError();
                }
                else
                {
                    if (offscreen)
                        GL.FrontFace(FrontFaceDirection.Ccw);
                    else
                        GL.FrontFace(FrontFaceDirection.Cw);
                    GraphicsExtensions.CheckGLError();
                }
            }

#if MONOMAC || WINDOWS || LINUX
			if (FillMode == FillMode.Solid) 
				GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            else
				GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
#else
            if (FillMode != FillMode.Solid)
                throw new NotImplementedException();
#endif

			if (ScissorTestEnable)
				GL.Enable(EnableCap.ScissorTest);
			else
				GL.Disable(EnableCap.ScissorTest);
            GraphicsExtensions.CheckGLError();

            if (this.DepthBias != 0 || this.SlopeScaleDepthBias != 0)
            {   
                GL.Enable(EnableCap.PolygonOffsetFill);
                GL.PolygonOffset(this.SlopeScaleDepthBias, this.DepthBias);
            }
            else
                GL.Disable(EnableCap.PolygonOffsetFill);
            GraphicsExtensions.CheckGLError();

            // TODO: Implement MultiSampleAntiAlias
        }

#elif DIRECTX

        protected internal override void GraphicsDeviceResetting()
        {
            SharpDX.Utilities.Dispose(ref _state);
            base.GraphicsDeviceResetting();
        }

        internal void ApplyState(GraphicsDevice device)
        {
            if (_state == null)
            {
                // We're now bound to a device... no one should
                // be changing the state of this object now!
                GraphicsDevice = device;

                // Build the description.
                var desc = new SharpDX.Direct3D11.RasterizerStateDescription();

                switch ( CullMode )
                {
                    case Graphics.CullMode.CullClockwiseFace:
                        desc.CullMode = SharpDX.Direct3D11.CullMode.Front;
                        break;

                    case Graphics.CullMode.CullCounterClockwiseFace:
                        desc.CullMode = SharpDX.Direct3D11.CullMode.Back;
                        break;

                    case Graphics.CullMode.None:
                        desc.CullMode = SharpDX.Direct3D11.CullMode.None;
                        break;
                }

                desc.IsScissorEnabled = ScissorTestEnable;
                desc.IsMultisampleEnabled = MultiSampleAntiAlias;
                desc.DepthBias = (int)DepthBias;
                desc.SlopeScaledDepthBias = SlopeScaleDepthBias;

                if (FillMode == Graphics.FillMode.WireFrame)
                    desc.FillMode = SharpDX.Direct3D11.FillMode.Wireframe;
                else
                    desc.FillMode = SharpDX.Direct3D11.FillMode.Solid;

                // These are new DX11 features we should consider exposing
                // as part of the extended MonoGame API.
                desc.IsFrontCounterClockwise = false;
                desc.IsAntialiasedLineEnabled = false;

                // To support feature level 9.1 these must 
                // be set to these exact values.
                desc.DepthBiasClamp = 0.0f;
                desc.IsDepthClipEnabled = true;

                // Create the state.
                _state = new SharpDX.Direct3D11.RasterizerState(GraphicsDevice._d3dDevice, desc);
            }

            Debug.Assert(GraphicsDevice == device, "The state was created for a different device!");

            // NOTE: We make the assumption here that the caller has
            // locked the d3dContext for us to use.

            // Apply the state!
            device._d3dContext.Rasterizer.State = _state;
        }

        internal static void ResetStates()
        {
            _cullClockwise.Reset();
            _cullCounterClockwise.Reset();
            _cullNone.Reset();
        }

#endif // DIRECTX
#if PSM
        static readonly Dictionary<CullMode, CullFaceMode> MapCullMode = new Dictionary<CullMode, CullFaceMode> {
            {CullMode.None, CullFaceMode.None},
            {CullMode.CullClockwiseFace, CullFaceMode.Front}, // Cull cw
            {CullMode.CullCounterClockwiseFace, CullFaceMode.Back}, // Cull ccw
        };
        
        internal void ApplyState(GraphicsDevice device)
        {
            var g = device.Context;
            
            g.SetCullFace(MapCullMode[CullMode], CullFaceDirection.Cw); // Front == cw
            g.Enable(EnableMode.CullFace, this.CullMode != CullMode.None);
            
            // FIXME: Everything else
        }
#endif
    }
}