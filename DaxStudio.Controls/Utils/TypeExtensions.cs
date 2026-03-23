using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Controls.Utils
{
    internal static class TypeExtensions
    {
        public static bool Satisfies(Type typeA, Type typeB)
        {
            var types = new List<Type>(typeA.GetInterfaces());
            for (var t = typeA; t != null; t = t.BaseType)
            {
                types.Add(t);
            }
            return types.Any(t =>
                t == typeB ||
                    t.IsGenericType && (t.GetGenericTypeDefinition() == typeB));
        }
    }
}
