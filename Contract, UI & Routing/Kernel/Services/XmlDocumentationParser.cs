using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using SoftwareCenter.Core.Discovery;

namespace SoftwareCenter.Kernel.Services
{
    /// <summary>
    /// Parses .NET XML documentation files to extract rich metadata.
    /// </summary>
    public class XmlDocumentationParser
    {
        private readonly XDocument _xmlDoc;

        private XmlDocumentationParser(XDocument xmlDoc)
        {
            _xmlDoc = xmlDoc;
        }

        public static bool TryCreateForAssembly(Assembly assembly, out XmlDocumentationParser parser)
        {
            parser = null;
            var assemblyPath = assembly.Location;
            if (string.IsNullOrEmpty(assemblyPath)) return false;

            var xmlPath = Path.ChangeExtension(assemblyPath, ".xml");
            if (!File.Exists(xmlPath)) return false;

            try
            {
                var doc = XDocument.Load(xmlPath);
                parser = new XmlDocumentationParser(doc);
                return true;
            }
            catch { return false; }
        }

        public string GetTypeSummary(Type type)
        {
            var key = $"T:{type.FullName}";
            var member = _xmlDoc.Descendants("member").FirstOrDefault(m => m.Attribute("name")?.Value == key);
            return member?.Element("summary")?.Value.Trim() ?? string.Empty;
        }

        public List<ParameterDescriptor> GetConstructorParameters(ConstructorInfo ctor)
        {
            var descriptors = new List<ParameterDescriptor>();
            var typeName = ctor.DeclaringType.FullName.Replace('+', '.');
            var paramTypeNames = ctor.GetParameters().Select(p => p.ParameterType.FullName);
            
            // Construct the XML member name for the constructor
            var memberNameBuilder = new StringBuilder($"M:{typeName}.#ctor");
            if (paramTypeNames.Any())
            {
                memberNameBuilder.Append($"({string.Join(",", paramTypeNames)})");
            }
            var memberName = memberNameBuilder.ToString();

            var memberElement = _xmlDoc.Descendants("member").FirstOrDefault(m => m.Attribute("name")?.Value == memberName);

            foreach (var paramInfo in ctor.GetParameters())
            {
                var paramSummary = memberElement?.Elements("param")
                    .FirstOrDefault(p => p.Attribute("name")?.Value == paramInfo.Name)?.Value.Trim() ?? "";

                descriptors.Add(new ParameterDescriptor(paramInfo.Name, paramInfo.ParameterType.FullName, paramSummary));
            }

            return descriptors;
        }

        /// <summary>
        /// Attempts to get the XML documentation description for a named parameter of a method.
        /// </summary>
        /// <param name="method">The method to inspect.</param>
        /// <param name="parameterName">The parameter name.</param>
        /// <returns>The parameter description or empty string if not found.</returns>
        public string GetParameterDescription(MethodInfo method, string parameterName)
        {
            if (method == null || string.IsNullOrEmpty(parameterName)) return string.Empty;

            var declaringType = method.DeclaringType.FullName.Replace('+', '.');
            var methodName = method.Name;

            // Build the member name for the method
            var paramTypeNames = method.GetParameters().Select(p => p.ParameterType.FullName).ToList();
            var memberNameBuilder = new StringBuilder($"M:{declaringType}.{methodName}");
            if (paramTypeNames.Any())
            {
                memberNameBuilder.Append($"({string.Join(",", paramTypeNames)})");
            }
            var memberName = memberNameBuilder.ToString();

            var memberElement = _xmlDoc.Descendants("member").FirstOrDefault(m => m.Attribute("name")?.Value == memberName);
            if (memberElement == null) return string.Empty;

            var paramElement = memberElement.Elements("param").FirstOrDefault(p => p.Attribute("name")?.Value == parameterName);
            return paramElement?.Value.Trim() ?? string.Empty;
        }
    }
}
