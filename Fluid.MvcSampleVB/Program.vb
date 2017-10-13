Imports System
Imports System.IO
Imports Microsoft.AspNetCore.Hosting

Module Program
    Sub Main(args As String())
        Call New WebHostBuilder().
            UseKestrel().
            UseContentRoot(Directory.GetCurrentDirectory()).
            UseIISIntegration().
            UseStartup(Of Startup)().
            Build().
            Run()
    End Sub
End Module
