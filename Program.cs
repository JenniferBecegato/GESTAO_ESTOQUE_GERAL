namespace GESTAO_ESTOQUE_GERAL;
internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if(args.Contains("--self-test")||args.Contains("--structure-test")) { SelfTest.Run(args.Contains("--structure-test")); return; }
        using var mutex = new Mutex(true,"Campos.GestaoEstoqueGeral",out var first);
        if(!first) { MessageBox.Show("O sistema de estoque já está aberto.","Campos"); return; }
        ApplicationConfiguration.Initialize();
        try { Application.Run(new Form1()); }
        catch(Exception ex) { MessageBox.Show("Não foi possível iniciar o sistema.\n" + ex.Message,"Campos • Estoque",MessageBoxButtons.OK,MessageBoxIcon.Error); }
    }
}
