namespace Fluid
{
    internal static class TemplateParseDiagnostics
    {
        internal static ParseException CreateException(string path, string errors)
        {
            return new ParseException($"Failed to parse template '{GetDisplayPath(path)}'.\n{errors}");
        }

        private static string GetDisplayPath(string path)
        {
            var isRooted =
                path.Length > 0 &&
                (path[0] == '/' ||
                 path[0] == '\\' ||
                 (path.Length > 1 && path[1] == ':' && char.IsLetter(path[0])));

            if (!isRooted)
            {
                return path;
            }

            var separator = path.LastIndexOfAny(['/', '\\']);
            return separator < 0 ? path : path.Substring(separator + 1);
        }
    }
}
