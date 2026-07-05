using System;

public class WpfDebug
{
    [STAThread]
    public static void Main()
    {
        try
        {
            LuminaControl.Program.Main();
        }
        catch (Exception ex)
        {
            Console.WriteLine("EXCEPTION: " + ex.ToString());
            if (ex.InnerException != null)
            {
                Console.WriteLine("INNER: " + ex.InnerException.ToString());
            }
        }
    }
}
