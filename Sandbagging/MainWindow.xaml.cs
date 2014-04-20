using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Linq;

namespace Sandbagging
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
        private Dictionary<string, DocMember> members;
        private void LoadDoc(string fileName)
        {
            XElement xml = XElement.Load(fileName);
            foreach (XElement memberNode in xml.Descendants("member"))
            {
                string name = (string)memberNode.Attribute("name");
                string prefix = name.Substring(0, 1);
                DocMember docMember = new DocMember();
                switch (prefix)
                {
                    case "T":
                        docMember.DocType = DocTypeEnum.Type;
                        break;
                    case "P":
                        docMember.DocType = DocTypeEnum.Property;
                        break;
                    case "M":
                        if (name.Contains("#ctor"))
                            docMember.DocType = DocTypeEnum.Constructor;
                        else
                            docMember.DocType = DocTypeEnum.Member;
                        break;
                    default:
                        // ToDo: warning
                        docMember.DocType = DocTypeEnum.Member;
                        break;
                }
                name = name.Substring(2);
                docMember.Summary = DocContent.ToDocContents(memberNode.Elements("summary").Nodes());
                docMember.Returns = string.Join("\n", memberNode.Elements("returns").Select(x => x.Value));
                foreach (XElement child in memberNode.Elements("param"))
                {
                    DocParam param = new DocParam();
                    param.Name = (string)child.Attribute("name");
                    param.Value = child.Value;
                    docMember.Params.Add(param);
                }
                if (docMember.DocType == DocTypeEnum.Type)
                {
                    docMember.Name = name;
                    members.Add(name, docMember);
                }
                else
                {
                    string parentTypeName;
                    if (docMember.DocType == DocTypeEnum.Constructor)
                    {
                        string[] parts = name.Split('#');
                        parentTypeName = parts.First().TrimEnd('.');
                        string constructor = GetUnqalified(parentTypeName) + parts.Last().Substring(4);
                        docMember.Name = constructor;
                    }
                    else
                    {
                        docMember.Name = GetUnqalified(name);
                        string[] parts = name.Split('.');
                        parentTypeName = string.Join(".", parts.Take(parts.Length - 1));
                    }
                    if (members.ContainsKey(parentTypeName))
                    {
                        members[parentTypeName].Children.Add(docMember);
                    }
                }
            }
        }
        private string GetUnqalified(string qualifiedName)
        {
            string[] parts = qualifiedName.Split('.');
            return parts.Last();
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            members = new Dictionary<string, DocMember>();
            LoadDoc(@"C:\Users\Jan\Documents\GitHub\Sandbagging\Sandbagging\bin\Debug\Sandbagging.XML");
            XElement xml = GenerateHtml();
            xml.Save(@"C:\Users\Jan\Documents\GitHub\Sandbagging\Sandbagging\bin\Debug\Sandbagging.html");
        }
        private XElement GenerateHtml()
        {
            XElement html = new XElement("html");
            XElement body = new XElement("body");
            html.Add(body);
            XElement style = new XElement("style");
            body.Add(style);
            style.Value = @"body {
  font-family:Arial;
}
table {
  border-spacing: 5px;
  border-collapse: collapse;
}
td,th {
  border: 1px solid #888;
  text-align: left;
  padding: 5px;
}
th {
  background-color: #A0FFC0;
}
td {
}
h1 {
  background-color: #207245;
  color: #FFFFFF;
}";
            foreach (KeyValuePair<string, DocMember> member in members)
            {
                if (member.Value.DocType == DocTypeEnum.Type)
                {
                    string qualifiedTypeName = member.Key;
                    body.Add(new XElement("h1", qualifiedTypeName));
                    AddChildren(body, DocTypeEnum.Constructor, member.Value, "Constructors");
                    AddChildren(body, DocTypeEnum.Property, member.Value, "Properties");
                    AddChildren(body, DocTypeEnum.Member, member.Value, "Methods");
                    // ToDo: Operators
                    // ToDo: Extension Methods
                }
            }
            return html;
        }
        private void AddChildren(XElement parentNode, DocTypeEnum docType, DocMember parentMember, string header)
        {
            IEnumerable<DocMember> children = parentMember.Children.Where(x => x.DocType == docType);
            if (children.Count() > 0)
            {
                parentNode.Add(new XElement("h2", header));
                XElement table = new XElement("table");
                parentNode.Add(table);
                XElement tr = new XElement("tr");
                table.Add(tr);
                tr.Add(new XElement("th", "Name"));
                tr.Add(new XElement("th", "Description"));
                foreach (DocMember childMember in children)
                {
                    tr = new XElement("tr");
                    table.Add(tr);
                    tr.Add(new XElement("td", childMember.Name));
                    XElement td = new XElement("td");
                    tr.Add(td);
                    foreach (DocContent docContent in childMember.Summary)
                    {
                        if (docContent is DocText)
                            td.Add(((DocText)docContent).Text);
                        else if (docContent is DocList)
                        {
                            DocList docList = (DocList)docContent;
                            if (docList.ListType == ListTypeEnum.Table)
                            {
                                // ToDo
                                table = new XElement("table");
                                td.Add(table);
                                tr = new XElement("tr");
                                table.Add(tr);
                                tr.Add(new XElement("th", docList.Header.Term));
                                tr.Add(new XElement("th", docList.Header.Description));
                                foreach (DocListItem item in docList.Items)
                                {
                                    tr = new XElement("tr");
                                    table.Add(tr);
                                    table.Add(new XElement("td", item.Term));
                                    table.Add(new XElement("td", item.Description));
                                }
                            }
                            else
                            {
                                XElement list;
                                if (docList.ListType == ListTypeEnum.Number)
                                    list = new XElement("ol");
                                else
                                    list = new XElement("ul");
                                td.Add(list);
                                foreach (DocListItem item in docList.Items)
                                {
                                    list.Add(new XElement("li", item.Description));
                                }
                            }
                        }
                    }
                }
            }
        }
    }
    public enum DocTypeEnum { Type, Member, Property, Constructor }
    /// <summary>
    /// Represents a member in Visual Studio xml documentation
    /// </summary>
    public class DocMember
    {
        public DocTypeEnum DocType { get; set; }
        /// <summary>
        /// The name of the member:
        /// <list type="bullet">
        /// <item> 
        /// <description>For types the name is qualified</description> 
        /// </item> 
        /// <item> 
        /// <description>For members the name is just the member name</description> 
        /// </item> 
        /// <item> 
        /// <description>For constructors the name is the unqualified type name with the parameters between parentheses.</description> 
        /// </item> 
        /// </list>
        /// The name should not be empty
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// The summaries of this member concatenated with newlines
        /// </summary>
        public List<DocContent> Summary { get; set; }
        /// <summary>
        /// The input parameters of this member
        /// </summary>
        public List<DocParam> Params { get; set; }
        /// <summary>
        /// The output parameter of this member
        /// </summary>
        public string Returns { get; set; }
        /// <summary>
        /// The children of this DocMember, can be M:, P: or .#ctor
        /// </summary>
        public List<DocMember> Children { get; set; }
        /// <summary>
        /// Create a new instance of DocMember
        /// </summary>
        public DocMember()
        {
            Params = new List<DocParam>();
            Children = new List<DocMember>();
            Summary = new List<DocContent>();
        }
        /// <summary>
        /// Create a new instance and set Name, Summary
        /// </summary>
        /// <param name="name"></param>
        /// <param name="summary"></param>
        public DocMember(string name, List<DocContent> summary)
        {
            Name = name;
            Summary = summary;
        }
    }
    public class DocParam
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }
    public class DocContent
    {
        internal static List<DocContent> ToDocContents(IEnumerable<XNode> nodes)
        {
            List<DocContent> result = new List<DocContent>();
            foreach (XNode node in nodes)
            {
                if (node.NodeType == System.Xml.XmlNodeType.Text)
                {
                    XText text = (XText)node;
                    DocText docText = new DocText();
                    docText.Text = text.Value;
                    result.Add(docText);
                }
                else if (node.NodeType == System.Xml.XmlNodeType.Element)
                {
                    XElement element = (XElement)node;
                    if (element.Name == "list")
                    {
                        DocList docList = new DocList();
                        result.Add(docList);
                        string listType = (string)element.Attribute("type");
                        switch (listType)
                        {
                            case "number": docList.ListType = ListTypeEnum.Number; break;
                            case "table": docList.ListType = ListTypeEnum.Table; break;
                            default: docList.ListType = ListTypeEnum.Bullet; break;
                        }
                        XElement header = element.Elements("listheader").FirstOrDefault();
                        if (header != null)
                        {
                            docList.Header.Term = GetNodeValue(element, "term");
                            docList.Header.Description = GetNodeValue(element, "description");
                        }
                        foreach (XElement child in element.Elements("item"))
                        {
                            docList.Items.Add(new DocListItem()
                            {
                                Term = GetNodeValue(child, "term"),
                                Description = GetNodeValue(child, "description"),
                            });
                        }
                    }
                }
            }
            return result;
        }
        public static string GetNodeValue(XElement xml, XName tag)
        {
            XElement firstChild = xml.Elements(tag).FirstOrDefault();
            if (firstChild != null)
                return firstChild.Value;
            return null;
        }
    }
    public class DocText : DocContent
    {
        public string Text { get; set; }
    }
    public enum ListTypeEnum { Bullet, Number, Table }
    public class DocList : DocContent
    {
        public ListTypeEnum ListType { get; set; }
        public DocListItem Header { get; set; }
        public List<DocListItem> Items { get; set; }
        public DocList()
        {
            Header = new DocListItem();
            Header.Term = "Name";
            Header.Description = "Value";
            Items = new List<DocListItem>();
        }
    }
    public class DocListItem
    {
        public string Term { get; set; }
        public string Description { get; set; }
    }
}
