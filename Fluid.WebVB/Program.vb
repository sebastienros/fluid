Imports System
Imports System.IO
Imports Microsoft.AspNetCore.Hosting

Module Program
    Sub Main(args As String())
        Dim host = New WebHostBuilder() _
            .UseKestrel() _
            .UseContentRoot(Directory.GetCurrentDirectory()) _
            .UseIISIntegration() _
            .UseStartup(Of Startup)() _
            .Build()

        host.Run()
    End Sub
End Module
