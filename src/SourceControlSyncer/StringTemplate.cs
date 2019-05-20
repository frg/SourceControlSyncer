using System.Collections.Generic;

namespace SourceControlSyncer
{
    internal static class StringTemplate
    {
        internal static string Compile(string template, Dictionary<string, string> variables)
        {
            var compiled = template;

            foreach (var variable in variables)
            {
                compiled = compiled.Replace($"{{{variable.Key}}}", variable.Value);
            }

            return compiled;
        }
    }
}