using SharpDX.Direct3D11;
using T3.Core.Operator;
using T3.Core.Operator.Attributes;
using T3.Core.Operator.Slots;

namespace T3.Operators.Types.Id_39370c54_5a17_414f_be8a_518dc25f09fe
{
    public class NewProject : Instance<NewProject>
    {
        [Output(Guid = "18d35a9d-8ee1-4b91-9ff2-bec518dc3f02")]
        public readonly Slot<Texture2D> ImgOutput = new();


    }
}

