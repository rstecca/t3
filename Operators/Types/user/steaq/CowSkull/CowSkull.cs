using SharpDX.Direct3D11;
using T3.Core.Operator;
using T3.Core.Operator.Attributes;
using T3.Core.Operator.Slots;

namespace T3.Operators.Types.Id_c627da8e_5d1b_486a_a8e0_31afcf4287f6
{
    public class CowSkull : Instance<CowSkull>
    {
        [Output(Guid = "c0389d95-5466-4f1c-a534-91f97e647efa")]
        public readonly Slot<Texture2D> ImgOutput = new();


    }
}

