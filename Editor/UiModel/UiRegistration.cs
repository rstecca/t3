
using T3.Core.DataTypes;
using T3.Core.DataTypes.DataSet;
using T3.Core.DataTypes.Vector;
using T3.Core.Operator;
using T3.Core.Rendering.Material;
using T3.Editor.Gui.InputUi;
using T3.Editor.Gui.InputUi.CombinedInputs;
using T3.Editor.Gui.InputUi.SimpleInputUis;
using T3.Editor.Gui.InputUi.SingleControl;
using T3.Editor.Gui.InputUi.VectorInputs;
using T3.Editor.Gui.OutputUi;
using Int3 = T3.Core.DataTypes.Vector.Int3;
using Point = T3.Core.DataTypes.Point;
using Texture2D = T3.Core.DataTypes.Texture2D;
using Texture3D = T3.Core.DataTypes.Texture3D;
using PixelShader = T3.Core.DataTypes.PixelShader;
using VertexShader = T3.Core.DataTypes.VertexShader;
using ComputeShader = T3.Core.DataTypes.ComputeShader;
using GeometryShader = T3.Core.DataTypes.GeometryShader;

namespace T3.Editor.UiModel;

internal static class UiRegistration
{
    public static void RegisterUiTypes()
    {
        var valueProperties = new ValueUiProperties();
        var shaderProperties = new ShaderUiProperties();
        var texProperties = new TextureUiProperties();
        var stringProperties = new StringUiProperties();
        var gpuProperties = new GpuUiProperties();
        
        // set colors of value types
        RegisterTypesToProperty(valueProperties, typeof(bool), typeof(int), typeof(float), typeof(double), typeof(DateTime), typeof(object),
                                typeof(Vector2), typeof(Vector3), typeof(Vector4), typeof(Quaternion), typeof(Int2), typeof(Int3), typeof(Int4),
                                typeof(Curve), typeof(Gradient), typeof(StructuredList), typeof(Dict<float>), typeof(List<float>),
                                typeof(Point[]), typeof(Vector4[]));
        
        TypeUiRegistry.SetProperties(typeof(string), stringProperties);
        TypeUiRegistry.SetProperties(typeof(List<string>), stringProperties);
        TypeUiRegistry.SetProperties(typeof(Command), new CommandUiProperties());

        RegisterTypesToProperty(shaderProperties,
                                typeof(ComputeShader),
                                typeof(GeometryShader),
                                typeof(PixelShader),
                                typeof(VertexShader),
                                typeof(Texture3D),

                                // sharpDX types
                                typeof(SharpDX.Direct3D11.BindFlags),
                                typeof(SharpDX.Direct3D11.BlendOperation),
                                typeof(SharpDX.Direct3D11.BlendOption),
                                typeof(SharpDX.Direct3D11.BlendState),
                                typeof(SharpDX.Direct3D11.Buffer),
                                typeof(SharpDX.Direct3D11.ColorWriteMaskFlags),
                                typeof(SharpDX.Direct3D11.Comparison),
                                typeof(SharpDX.Direct3D11.CpuAccessFlags),
                                typeof(SharpDX.Direct3D11.CullMode),
                                typeof(SharpDX.Direct3D11.DepthStencilState),
                                typeof(SharpDX.Direct3D11.DepthStencilView),
                                typeof(SharpDX.Direct3D11.FillMode),
                                typeof(SharpDX.Direct3D11.Filter),
                                typeof(SharpDX.Direct3D11.InputLayout),
                                typeof(SharpDX.Direct3D11.RasterizerState),
                                typeof(SharpDX.Direct3D11.ResourceOptionFlags),
                                typeof(SharpDX.Direct3D11.ResourceUsage),
                                typeof(SharpDX.Direct3D11.SamplerState),
                                typeof(SharpDX.Direct3D11.TextureAddressMode),
                                typeof(SharpDX.Direct3D11.UnorderedAccessViewBufferFlags),
                                typeof(SharpDX.DXGI.Format),
                                typeof(SharpDX.Mathematics.Interop.RawViewportF),
                                typeof(SharpDX.Mathematics.Interop.RawRectangle)
                               );

        RegisterTypesToProperty(texProperties, 
                                typeof(Texture2D),
                                typeof(RenderTargetReference),

                                // sharpDX types
                                typeof(SharpDX.Direct3D11.DepthStencilView),
                                typeof(SharpDX.Direct3D11.RenderTargetBlendDescription),
                                typeof(SharpDX.Direct3D11.RenderTargetView),
                                typeof(SharpDX.Direct3D11.ShaderResourceView),
                                typeof(SharpDX.Direct3D11.UnorderedAccessView)
                               );
        
        RegisterTypesToProperty(gpuProperties,
                                typeof(DataSet), typeof(SceneSetup), typeof(PbrMaterial), typeof(Texture3dWithViews), typeof(MeshBuffers), 
                                typeof(ParticleSystem), typeof(GizmoVisibility),
                                
                                // sharpDX types
                                typeof(SharpDX.Direct3D.PrimitiveTopology));
        
        // set colors of input and output UIs
        
        // system types
        RegisterIOType(typeof(float), () => new FloatInputUi(), () => new FloatOutputUi());
        RegisterIOType(typeof(int), () => new IntInputUi());
        RegisterIOType(typeof(bool), () => new BoolInputUi());
        RegisterIOType(typeof(double), () => new FloatInputUi(), () => new FloatOutputUi());

        RegisterIOType(typeof(Vector2), () => new Vector2InputUi(), () => new VectorOutputUi<Vector2>());
        RegisterIOType(typeof(Vector3), () => new Vector3InputUi(), () => new VectorOutputUi<Vector3>());
        RegisterIOType(typeof(Vector4), () => new Vector4InputUi(), () => new VectorOutputUi<Vector4>());
        RegisterIOType(typeof(Quaternion), () => new Vector4InputUi(), () => new VectorOutputUi<Quaternion>());

        RegisterIOType(typeof(List<float>), () => new FloatListInputUi(), () => new FloatListOutputUi());
        RegisterIOType(typeof(string), () => new StringInputUi());
        RegisterIOType(typeof(List<string>), () => new StringListInputUi(), () => new StringListOutputUi());;


        // t3 core types
        RegisterIOType(typeof(Curve), () => new CurveInputUi());
        RegisterIOType(typeof(Gradient), () => new GradientInputUi());
        RegisterIOType(typeof(Point[]), null, () => new PointArrayOutputUi());
        RegisterIOType(typeof(StructuredList), () => new StructuredListInputUi(), () => new StructuredListOutputUi());
        RegisterIOType(typeof(Int3), () => new Int3InputUi());
        RegisterIOType(typeof(Int2),() => new Int2InputUi());
        RegisterIOType(typeof(Dict<float>), () => new FloatDictInputUi(), () => new FloatDictOutputUi());
        

        // Rendering
        RegisterIOType(typeof(DataSet), null, () => new DataSetOutputUi());
        RegisterIOType(typeof(Texture2D), null, () => new Texture2dOutputUi());
        RegisterIOType(typeof(Texture3dWithViews), null, () => new Texture3dOutputUi());
        RegisterIOType(typeof(Command), null, () => new CommandOutputUi());
        RegisterIOType(typeof(BufferWithViews), null, () => new BufferWithViewsOutputUi());
        RegisterIOType(typeof(SceneSetup), () => new SceneSetupInputUi(), () => new SceneSetupOutputUi());

        return;

        static void RegisterTypesToProperty(ITypeUiProperties properties, params Type[] types)
        {
            foreach(var type in types)
                TypeUiRegistry.SetProperties(type, properties);
        }

        static void RegisterIOType(Type type, Func<IInputUi> inputUi, Func<IOutputUi> outputUi = null)
        {
            InputUiFactory.Instance.AddFactory(type, inputUi);
            OutputUiFactory.Instance.AddFactory(type, outputUi);
        }
    }
}