Imports Microsoft.AspNetCore.Mvc

Namespace Controllers
    Public Class HomeController
        Inherits Controller

        Public Function Index() As IActionResult
            Dim model As New List(Of Person) From {
                New Person With {.Firstname = "Bill", .Lastname = "Gates"},
                New Person With {.Firstname = "Steve", .Lastname = "Balmer"}
            }

            ViewData("Title") = "This is a title"

            Return View(model)
        End Function

    End Class
End Namespace
