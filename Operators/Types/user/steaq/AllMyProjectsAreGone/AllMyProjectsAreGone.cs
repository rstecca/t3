using SharpDX.Direct3D11;
using T3.Core.Operator;
using T3.Core.Operator.Attributes;
using T3.Core.Operator.Slots;

namespace T3.Operators.Types.Id_6700aa4b_f45b_40a3_af75_1fafc32049ef
{
    public class AllMyProjectsAreGone : Instance<AllMyProjectsAreGone>
    {
        [Output(Guid = "a4140edb-3338-46cc-85dc-95713d005a5e")]
        public readonly Slot<Texture2D> ImgOutput = new();


    }
}

