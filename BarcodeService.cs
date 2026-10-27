using System.Globalization;
using System.Security.Cryptography;
using System.Text;
namespace GESTAO_ESTOQUE_GERAL;
internal static class BarcodeService
{
    internal static string Generate(Inventory inventory)
    {
        var codes=inventory.Products.SelectMany(p=>new[]{p.Code}.Concat(p.Packages.Select(x=>x.Code))).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for(int attempt=0;attempt<100;attempt++)
        {
            string body="20"+RandomNumberGenerator.GetInt32(1000000000).ToString("D9",CultureInfo.InvariantCulture)+RandomNumberGenerator.GetInt32(10);
            int sum=body.Select((c,i)=>(c-'0')*(i%2==0?1:3)).Sum();string code=body+((10-sum%10)%10);
            if(!codes.Contains(code))return code;
        }
        throw new InvalidOperationException("Não foi possível gerar um código único. Tente novamente.");
    }
    internal static int[] Encode(string code)
    {
        if(string.IsNullOrWhiteSpace(code)||code.Length>80||code.Any(c=>c<32||c>126))throw new ArgumentException("A etiqueta aceita códigos de 1 a 80 caracteres sem acentos (letras, números e símbolos ASCII).");
        var symbols=new List<int>{104};int checksum=104;
        for(int i=0;i<code.Length;i++){int value=code[i]-32;symbols.Add(value);checksum+=value*(i+1);}
        symbols.Add(checksum%103);symbols.Add(106);return symbols.ToArray();
    }
    internal static string Svg(string code)
    {
        if(code.Length==13&&code.All(c=>c>='0'&&c<='9')&&code.Select((c,i)=>(c-'0')*(i%2==0?1:3)).Sum()%10==0)return Ean13(code);
        var patterns=LegacyForm.BarcodePatterns.Split(' ');var symbols=Encode(code);int width=symbols.Sum(v=>patterns[v].Sum(c=>c-'0'))+20,x=10;
        var bars=new StringBuilder();
        foreach(int value in symbols)
        {
            bool black=true;foreach(char digit in patterns[value]){int w=digit-'0';if(black)bars.Append(CultureInfo.InvariantCulture,$"<rect x=\"{x}\" y=\"0\" width=\"{w}\" height=\"55\"/>");x+=w;black=!black;}
        }
        return $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {width} 55\" width=\"{width}\" height=\"55\" preserveAspectRatio=\"none\" shape-rendering=\"crispEdges\"><rect width=\"100%\" height=\"100%\" fill=\"white\"/><g fill=\"black\">{bars}</g></svg>";
    }
    private static string Ean13(string code)
    {
        string[] left=["0001101","0011001","0010011","0111101","0100011","0110001","0101111","0111011","0110111","0001011"];
        string[] parity=["LLLLLL","LLGLGG","LLGGLG","LLGGGL","LGLLGG","LGGLLG","LGGGLL","LGLGLG","LGLGGL","LGGLGL"];
        string bits="101";
        for(int i=1;i<=6;i++){string l=left[code[i]-'0'];bits+=parity[code[0]-'0'][i-1]=='L'?l:new string(l.Reverse().Select(c=>c=='0'?'1':'0').ToArray());}
        bits+="01010";for(int i=7;i<13;i++)bits+=new string(left[code[i]-'0'].Select(c=>c=='0'?'1':'0').ToArray());bits+="101";
        var bars=new StringBuilder();for(int i=0;i<bits.Length;i++)if(bits[i]=='1')bars.Append(CultureInfo.InvariantCulture,$"<rect x=\"{i+11}\" y=\"0\" width=\"1\" height=\"55\"/>");
        return $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 113 55\" width=\"113\" height=\"55\" preserveAspectRatio=\"none\" shape-rendering=\"crispEdges\"><rect width=\"100%\" height=\"100%\" fill=\"white\"/><g fill=\"black\">{bars}</g></svg>";
    }
}

