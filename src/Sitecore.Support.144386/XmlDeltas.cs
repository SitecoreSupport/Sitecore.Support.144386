using Sitecore.Data.Fields;
using Sitecore.Diagnostics;
using Sitecore.Xml;
using Sitecore.Xml.Patch;
using System;
using System.Xml;

namespace Sitecore.Support.Data.Fields
{
    /// <summary>
    /// Utility class that implements support for delta values in XML fields such as the <see cref="T:Sitecore.Data.Fields.LayoutField" />
    /// </summary>
    public static class XmlDeltas
    {
        /// <summary>
        /// Gets the standard value for the specified field.
        /// </summary>
        /// <param name="field">The field to return standard value for.</param>
        /// <returns>The standard value for the field.</returns>
        public static string GetStandardValue(Field field)
        {
            return field.GetStandardValue();
        }

        /// <summary>
        /// Can be used together with <see cref="M:Sitecore.Data.Fields.XmlDeltas.GetFieldValue(Sitecore.Data.Fields.Field,System.Func{Sitecore.Data.Fields.Field,System.String})" /> to provide a default 
        /// implementation of getBaseValue parameter with a specific value to be used 
        /// in place of empty field values.
        /// </summary>
        /// <param name="emptyValue">The default XML markup of an empty value of the XML field.</param>
        /// <returns>The function which will provide standard value or empty value for the field.</returns>
        public static Func<Field, string> WithEmptyValue(string emptyValue)
        {
            return delegate(Field field)
            {
                string standardValue = field.GetStandardValue();
                if (standardValue == null || standardValue.Trim().Length == 0)
                {
                    return emptyValue;
                }
                return standardValue;
            };
        }

        /// <summary>
        /// Gets the layout value, applying Layout Delta if necessary.
        /// </summary>
        /// <param name="field">The field to get delta value for.</param>
        /// <param name="getBaseValue">The function which will return a base value to calculate delta.</param>
        /// <returns>The layout value.</returns>
        public static string GetFieldValue(Field field, Func<Field, string> getBaseValue)
        {
            Assert.ArgumentNotNull(field, "field");
            Assert.ArgumentNotNull(getBaseValue, "getBaseValue");
            string value = field.GetValue(false, false);
            if (string.IsNullOrEmpty(value))
            {
                return field.Value;
            }
            if (XmlPatchUtils.IsXmlPatch(value))
            {
                return XmlDeltas.ApplyDelta(getBaseValue(field), value);
            }
            return Assert.ResultNotNull<string>(value);
        }

        /// <summary>
        /// Sets the field value.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        public static void SetFieldValue(Field field, string value)
        {
            Assert.ArgumentNotNull(field, "field");
            Assert.ArgumentNotNull(value, "value");
            if (field.Item.Name == "__Standard Values")
            {
                field.Value = value;
                return;
            }
            field.Value = XmlDeltas.GetDelta(value, field.GetStandardValue());
        }

        /// <summary>
        /// Applies the delta to a base value.
        /// </summary>
        /// <param name="baseValue">The base value.</param>
        /// <param name="delta">The delta value.</param>
        /// <returns>The final XML markup.</returns>
        public static string ApplyDelta(string baseValue, string delta)
        {
            Assert.ArgumentNotNull(baseValue, "baseValue");
            System.Xml.XmlDocument xmlDocument = XmlUtil.LoadXml(delta);
            Assert.IsNotNull(xmlDocument, "Layout Delta is not a valid XML");
            System.Xml.XmlNode documentElement = xmlDocument.DocumentElement;
            Assert.IsNotNull(documentElement, "Xml document root element is missing (delta)");
            System.Xml.XmlDocument xmlDocument2 = XmlUtil.LoadXml(baseValue);
            Assert.IsNotNull(xmlDocument2, "Layout Value is not a valid XML");
            System.Xml.XmlNode documentElement2 = xmlDocument2.DocumentElement;
            Assert.IsNotNull(documentElement2, "Xml document root element is missing (base)");
            new XmlPatcher("s", "p").Merge(documentElement2, documentElement);
            return documentElement2.OuterXml;
        }

        /// <summary>
        /// Gets the delta for a specific layout field.
        /// </summary>
        /// <param name="layoutValue">The layout value entered though page editor or layout details dialog.</param>
        /// <param name="baseValue">The base layout value against which to generate the delta.</param>
        /// <returns>The layout diff to store in the field.</returns>
        public static string GetDelta(string layoutValue, string baseValue)
        {
            System.Xml.XmlDocument xmlDocument = XmlUtil.LoadXml(baseValue);
            if (xmlDocument != null)
            {
                System.Xml.XmlDocument xmlDocument2 = XmlUtil.LoadXml(layoutValue);
                if (xmlDocument2 != null)
                {
                    System.Xml.XmlDocument xmlDocument3 = Sitecore.Support.Xml.Patch.XmlDiffUtils.Compare(xmlDocument, xmlDocument2, new ElementIdentification(), new XmlPatchNamespaces
                    {
                        PatchNamespace = "p",
                        SetNamespace = "s"
                    });
                    layoutValue = xmlDocument3.OuterXml;
                }
            }
            return layoutValue;
        }
    }
}