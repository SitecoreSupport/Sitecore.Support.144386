using Sitecore.Diagnostics;
using Sitecore.Text.Diff;
using Sitecore.Xml.Patch;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace Sitecore.Support.Xml.Patch
{
    /// <summary>
    /// Implements utility methods to generate XML patches.
    /// </summary>
    public class XmlDiffUtils
    {
        /// <summary>
        /// Compares two XML sources and records changes in a comparison context.
        /// </summary>
        /// <param name="original">The original XML document.</param>
        /// <param name="modified">The modified XML document.</param>
        /// <param name="identification">The identification policy for the document.</param>
        /// <param name="context">The comparison context to record changes.</param>
        /// <exception cref="T:System.Exception">Can't start with unequal nodes</exception>
        public static void Compare(IXmlElement original, IXmlElement modified, IElementIdentification identification, IComparisonContext context)
        {
            if (identification.GetID(original) != identification.GetID(modified))
            {
                throw new System.Exception("Can't start with unequal nodes");
            }
            context.SetIdentification(identification.GetSignificantAttributes(modified));
            XmlDiffUtils.CompareAttributes(original, modified, context);
            XmlDiffUtils.CompareChildren(original, modified, identification, context);
        }

        /// <summary>
        /// Compares the specified original.
        /// </summary>
        /// <param name="original">The original XML document.</param>
        /// <param name="modified">The modified XML document.</param>
        /// <param name="id">The element identification policy for the document.</param>
        /// <param name="ns">The namespaces to use when generating the patch.</param>
        /// <returns>The compared XML from two XML sources.</returns>
        public static System.Xml.XmlDocument Compare(System.Xml.XmlDocument original, System.Xml.XmlDocument modified, IElementIdentification id, XmlPatchNamespaces ns)
        {
            Assert.IsNotNull(original, "Failed to load original XML");
            Assert.IsNotNull(modified, "Failed to load modified XML");
            System.Xml.XmlDocument xmlDocument = new System.Xml.XmlDocument();
            System.Xml.XmlNode documentElement = original.DocumentElement;
            System.Xml.XmlNode documentElement2 = modified.DocumentElement;
            xmlDocument.AppendChild(xmlDocument.CreateElement(documentElement.Prefix, documentElement.LocalName, documentElement.NamespaceURI));
            XmlDiffUtils.Compare(new XmlDomSource(documentElement), new XmlDomSource(documentElement2), id, new XmlElementContext(xmlDocument.DocumentElement, ns));
            return xmlDocument;
        }

        /// <summary>
        /// Compares the attributes of two XML elements.
        /// </summary>
        /// <param name="original">The original element.</param>
        /// <param name="modified">The modified element.</param>
        /// <param name="context">The comparison context to record the changes.</param>
        private static void CompareAttributes(IXmlElement original, IXmlElement modified, IComparisonContext context)
        {
            IXmlNode[] attributes = (from node in original.GetAttributes()
                                     orderby node.NamespaceURI + ":" + node.LocalName
                                     select node).ToArray<IXmlNode>();
            IXmlNode[] array = (from node in modified.GetAttributes()
                                orderby node.NamespaceURI + ":" + node.LocalName
                                select node).ToArray<IXmlNode>();
            DiffEngine diffEngine = new DiffEngine();
            diffEngine.ProcessDiff(new AttributeDiffList(attributes), new AttributeDiffList(array));
            foreach (DiffResultSpan diffResultSpan in diffEngine.DiffReport())
            {
                if (diffResultSpan.Status == DiffResultSpanStatus.AddDestination || diffResultSpan.Status == DiffResultSpanStatus.Replace)
                {
                    for (int i = 0; i < diffResultSpan.Length; i++)
                    {
                        context.SetAttribute(array[diffResultSpan.DestIndex + i]);
                    }
                }
            }
        }

        /// <summary>
        /// Compares the child elements of two XML nodes.
        /// </summary>
        /// <param name="original">The original element.</param>
        /// <param name="modified">The modified element.</param>
        /// <param name="id">The element identification policy of the document.</param>
        /// <param name="context">The comparison context.</param>
        private static void CompareChildren(IXmlElement original, IXmlElement modified, IElementIdentification id, IComparisonContext context)
        {
            IXmlElement[] array = original.GetChildren().ToArray<IXmlElement>();
            IXmlElement[] array2 = modified.GetChildren().ToArray<IXmlElement>();
            DiffEngine diffEngine = new DiffEngine();
            diffEngine.ProcessDiff(new ElementDiffList(array, id), new ElementDiffList(array2, id));
            foreach (DiffResultSpan current in XmlDiffUtils.Postprocess(diffEngine.DiffReport(), array, array2, id))
            {
                bool flag = current.Status == DiffResultSpanStatus.DeleteSource || current.Status == DiffResultSpanStatus.Replace;
                bool flag2 = current.Status == DiffResultSpanStatus.AddDestination || current.Status == DiffResultSpanStatus.Replace;
                bool flag3 = current.Status == DiffResultSpanStatus.NoChange;
                if (flag && current.Link == null)
                {
                    for (int i = 0; i < current.Length; i++)
                    {
                        IXmlElement xmlElement = array[current.SourceIndex + i];
                        IComparisonContext childContext = context.GetChildContext(xmlElement.LocalName);
                        childContext.SetIdentification(id.GetSignificantAttributes(xmlElement));
                        childContext.Delete();
                    }
                }
                if (flag2)
                {
                    IXmlElement xmlElement2 = null;
                    if (current.DestIndex + current.Length < array2.Length)
                    {
                        xmlElement2 = array2[current.DestIndex + current.Length];
                    }
                    if (current.Link == null)
                    {
                        for (int j = 0; j < current.Length; j++)
                        {
                            XmlDiffUtils.InsertNode(context, array2[current.DestIndex + j], xmlElement2, id);
                        }
                    }
                    else
                    {
                        Assert.IsTrue(current.Length == 1, "When moving, expect span.Length = 1");
                        IXmlElement original2 = array[current.Link.SourceIndex];
                        IXmlElement xmlElement3 = array2[current.DestIndex];
                        IComparisonContext childContext2 = context.GetChildContext(xmlElement3.LocalName);
                        childContext2.SetIdentification(id.GetSignificantAttributes(xmlElement3));
                        string reference = "*[1=2]";
                        if (xmlElement2 != null)
                        {
                            reference = XmlDiffUtils.GetXPath(xmlElement2, id);
                        }
                        childContext2.SetInsertOption("before", reference);
                        childContext2.Materialize();
                        XmlDiffUtils.CompareAttributes(original2, xmlElement3, childContext2);
                        XmlDiffUtils.CompareChildren(original2, xmlElement3, id, childContext2);
                    }
                }
                if (flag3)
                {
                    for (int k = 0; k < current.Length; k++)
                    {
                        IXmlElement original3 = array[current.SourceIndex + k];
                        IXmlElement xmlElement4 = array2[current.DestIndex + k];
                        IComparisonContext childContext3 = context.GetChildContext(xmlElement4.LocalName);
                        childContext3.SetIdentification(id.GetSignificantAttributes(xmlElement4));
                        XmlDiffUtils.CompareAttributes(original3, xmlElement4, childContext3);
                        XmlDiffUtils.CompareChildren(original3, xmlElement4, id, childContext3);
                    }
                }
            }
        }

        /// <summary>
        /// Post processes the specified report.
        /// </summary>
        /// <param name="report">The report.</param>
        /// <param name="source">The source.</param>
        /// <param name="modified">The modified.</param>
        /// <param name="id">The element id.</param>
        /// <returns>
        /// The I enumerable.
        /// </returns>
        /// <exception cref="T:System.Exception"><c>Exception</c>.</exception>
        private static System.Collections.Generic.IEnumerable<DiffResultSpan> Postprocess(System.Collections.ArrayList report, IXmlElement[] source, IXmlElement[] modified, IElementIdentification id)
        {
            System.Collections.Generic.List<DiffResultSpan> list = new System.Collections.Generic.List<DiffResultSpan>();
            foreach (DiffResultSpan diffResultSpan in report)
            {
                if (diffResultSpan.Status == DiffResultSpanStatus.Replace)
                {
                    list.Add(DiffResultSpan.CreateDeleteSource(diffResultSpan.SourceIndex, diffResultSpan.Length));
                    list.Add(DiffResultSpan.CreateAddDestination(diffResultSpan.DestIndex, diffResultSpan.Length));
                }
                else
                {
                    list.Add(diffResultSpan);
                }
            }
            System.Collections.Generic.List<DiffResultSpan> list2 = new System.Collections.Generic.List<DiffResultSpan>();
            foreach (DiffResultSpan current in list)
            {
                if (current.Status == DiffResultSpanStatus.NoChange || current.Length == 1)
                {
                    list2.Add(current);
                }
                else
                {
                    for (int i = 0; i < current.Length; i++)
                    {
                        if (current.Status == DiffResultSpanStatus.DeleteSource)
                        {
                            list2.Add(DiffResultSpan.CreateDeleteSource(current.SourceIndex + i, 1));
                        }
                        else
                        {
                            if (current.Status != DiffResultSpanStatus.AddDestination)
                            {
                                throw new System.Exception();
                            }
                            list2.Add(DiffResultSpan.CreateAddDestination(current.DestIndex + i, 1));
                        }
                    }
                }
            }
            System.Collections.Generic.List<DiffResultSpan> list3 = new System.Collections.Generic.List<DiffResultSpan>();
            System.Collections.Generic.List<DiffResultSpan> list4 = new System.Collections.Generic.List<DiffResultSpan>();
            foreach (DiffResultSpan current2 in list2)
            {
                if (current2.Status == DiffResultSpanStatus.AddDestination)
                {
                    bool flag = false;
                    foreach (DiffResultSpan current3 in list3)
                    {
                        if (current3.Link == null && id.GetID(source[current3.SourceIndex]) == id.GetID(modified[current2.DestIndex]))
                        {
                            current3.SetLink(current2);
                            flag = true;
                            break;
                        }
                    }
                    if (!flag)
                    {
                        list4.Add(current2);
                    }
                }
                if (current2.Status == DiffResultSpanStatus.DeleteSource)
                {
                    bool flag2 = false;
                    foreach (DiffResultSpan current4 in list4)
                    {
                        if (current4.Link == null && id.GetID(source[current2.SourceIndex]) == id.GetID(modified[current4.DestIndex]))
                        {
                            current4.SetLink(current2);
                            flag2 = true;
                            break;
                        }
                    }
                    if (!flag2)
                    {
                        list3.Add(current2);
                    }
                }
            }
            return list2;
        }

        /// <summary>
        /// Inserts the node into comparison context.
        /// </summary>
        /// <param name="context">The comparison context.</param>
        /// <param name="element">The element to insert.</param>
        /// <param name="reference">The reference element before which to insert the target element.</param>
        /// <param name="id">The identification policy of the document.</param>
        private static void InsertNode(IComparisonContext context, IXmlElement element, IXmlElement reference, IElementIdentification id)
        {
            IComparisonContext childContext = context.GetChildContext(element.LocalName);
            childContext.SetIdentification(id.GetSignificantAttributes(element));
            if (reference != null)
            {
                childContext.SetInsertOption("before", XmlDiffUtils.GetXPath(reference, id));
            }
            childContext.Materialize();
            using (System.Collections.Generic.IEnumerator<IXmlNode> enumerator = element.GetAttributes().GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    IXmlNode attribute = enumerator.Current;
                    if (!id.GetSignificantAttributes(element).Any((IXmlNode a) => a.NamespaceURI + a.LocalName == attribute.NamespaceURI + attribute.LocalName))
                    {
                        childContext.SetAttribute(attribute);
                    }
                }
            }
            foreach (IXmlElement current in element.GetChildren())
            {
                XmlDiffUtils.InsertNode(childContext, current, null, id);
            }
        }

        /// <summary>
        /// Gets the XPath to identify a specific element.
        /// </summary>
        /// <param name="element">The element to identify.</param>
        /// <param name="id">The element id.</param>
        /// <returns>The xpath of the element.</returns>
        private static string GetXPath(IXmlElement element, IElementIdentification id)
        {
            System.Text.StringBuilder stringBuilder = new System.Text.StringBuilder(element.LocalName);
            bool flag = false;
            foreach (IXmlNode current in id.GetSignificantAttributes(element))
            {
                if (!flag)
                {
                    stringBuilder.Append("[");
                    flag = true;
                }
                else
                {
                    stringBuilder.Append(" and ");
                }
                stringBuilder.Append("@");
                stringBuilder.Append(current.LocalName);
                stringBuilder.Append("='");
                stringBuilder.Append(current.Value);
                stringBuilder.Append("'");
            }
            if (flag)
            {
                stringBuilder.Append("]");
            }
            return stringBuilder.ToString();
        }
    }
}