using System.Security.Cryptography;
using System.Text;
using Aida.Engine.Numerics;

namespace Aida.Engine.Tests.Numerics;

public sealed class AorPowTablesTests
{
    [Fact]
    public void Las_tablas_son_las_del_anexo()
    {
        var sb = new StringBuilder();
        sb.Append($"log.ln2hi 0x{BitConverter.DoubleToUInt64Bits(AorPowLogTable.Ln2Hi):X16}\n");
        sb.Append($"log.ln2lo 0x{BitConverter.DoubleToUInt64Bits(AorPowLogTable.Ln2Lo):X16}\n");

        for (var i = 0; i < AorPowLogTable.Poly.Length; i++)
        {
            sb.Append($"log.poly[{i}] 0x{BitConverter.DoubleToUInt64Bits(AorPowLogTable.Poly[i]):X16}\n");
        }

        for (var i = 0; i < AorPowLogTable.InvC.Length; i++)
        {
            sb.Append($"log.tab[{i}] invc=0x{BitConverter.DoubleToUInt64Bits(AorPowLogTable.InvC[i]):X16} logc=0x{BitConverter.DoubleToUInt64Bits(AorPowLogTable.LogC[i]):X16} logctail=0x{BitConverter.DoubleToUInt64Bits(AorPowLogTable.LogCTail[i]):X16}\n");
        }

        sb.Append($"exp.invln2N 0x{BitConverter.DoubleToUInt64Bits(AorExpTable.InvLn2N):X16}\n");
        sb.Append($"exp.negln2hiN 0x{BitConverter.DoubleToUInt64Bits(AorExpTable.NegLn2HiN):X16}\n");
        sb.Append($"exp.negln2loN 0x{BitConverter.DoubleToUInt64Bits(AorExpTable.NegLn2LoN):X16}\n");
        sb.Append($"exp.shift 0x{BitConverter.DoubleToUInt64Bits(AorExpTable.Shift):X16}\n");

        for (var i = 0; i < AorExpTable.Poly.Length; i++)
        {
            sb.Append($"exp.poly[{i}] 0x{BitConverter.DoubleToUInt64Bits(AorExpTable.Poly[i]):X16}\n");
        }

        for (var i = 0; i < AorExpTable.Tab.Length; i++)
        {
            sb.Append($"exp.tab[{i}] 0x{AorExpTable.Tab[i]:X16}\n");
        }

        var text = sb.ToString();
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(401, lines.Length);

        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        Assert.Equal("455927e49676ae67ecb1d61667a0af0b9923a1788bd7fcb54f0473bed4be3e6e", hash);
    }
}
