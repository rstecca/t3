using SharpDX.Direct3D11;
using T3.Core.Operator;
using T3.Core.Operator.Attributes;
using T3.Core.Operator.Slots;

namespace T3.Operators.Types.Id_d819ae44_d351_411d_80c9_5d6e1793f7c7
{
    public class Cornery : Instance<Cornery>
    {
        [Output(Guid = "66b32e0f-0029-4a03-91e8-9678d3b5dc86")]
        public readonly Slot<Texture2D> ImgOutput = new();


    }
}

