Imports System
Imports System.IO
Imports Microsoft.AspNetCore.Hosting

Module Program
    Sub Main(args As String())
        Dim host = New WebHostBuilder().
            UseKestrel().
            UseContentRoot(Directory.GetCurrentDirectory()).
            UseIISIntegration().
            UseStartup(Of Startup)().
            Build()

        host.Run()
    End Sub
End Module
