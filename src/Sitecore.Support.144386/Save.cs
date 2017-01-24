using Sitecore.Collections;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Data.Validators;
using Sitecore.Diagnostics;
using Sitecore.Pipelines;
using Sitecore.Pipelines.Save;
using Sitecore.Shell.Applications.WebEdit.Commands;
using Sitecore.Shell.Framework.Commands;
using Sitecore.Web;
using Sitecore.Web.UI.Sheer;
using Sitecore.Xml;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Web;
using System.Web.UI;
using System.Xml;

namespace Sitecore.Support.Shell.Applications.WebEdit.Commands
{
    [Serializable]
    public class Save : Sitecore.Shell.Applications.WebEdit.Commands.Save
    {
        private static void AddLayoutField(Page page, Packet packet, Item item)
        {
            Assert.ArgumentNotNull(page, "page");
            Assert.ArgumentNotNull(packet, "packet");
            Assert.ArgumentNotNull(item, "item");
            string text = page.Request.Form["scLayout"];
            if (!string.IsNullOrEmpty(text))
            {
                text = WebEditUtil.ConvertJSONLayoutToXML(text);
                Assert.IsNotNull(text, text);
                if (item.Name != "__Standard Values")
                {
                    text = Sitecore.Support.Data.Fields.XmlDeltas.GetDelta(text, item.Fields[FieldIDs.LayoutField].GetStandardValue());
                }
                packet.StartElement("field");
                packet.SetAttribute("itemid", item.ID.ToString());
                packet.SetAttribute("language", item.Language.ToString());
                packet.SetAttribute("version", item.Version.ToString());
                packet.SetAttribute("fieldid", FieldIDs.LayoutField.ToString());
                packet.AddElement("value", text, new string[0]);
                packet.EndElement();
            }
        }

        private static Packet CreatePacket(IEnumerable<PageEditorField> fields, out SafeDictionary<FieldDescriptor, string> controlsToValidate)
        {
            MethodInfo method = typeof(Sitecore.Shell.Applications.WebEdit.Commands.Save).GetMethod("CreatePacket", BindingFlags.Static | BindingFlags.NonPublic, null, new Type[]
			{
				typeof(IEnumerable<PageEditorField>),
				typeof(SafeDictionary<FieldDescriptor, string>).MakeByRefType()
			}, null);
            object[] array = new object[]
			{
				fields,
				null
			};
            Packet result = (Packet)method.Invoke(null, array);
            controlsToValidate = (SafeDictionary<FieldDescriptor, string>)array[1];
            return result;
        }

        public override void Execute(CommandContext context)
        {
            Assert.ArgumentNotNull(context, "context");
            HttpContext current = HttpContext.Current;
            if (current != null)
            {
                Page page = current.Handler as Page;
                if (page != null && context.Items.Length == 1)
                {
                    IEnumerable<PageEditorField> fields = WebEditCommand.GetFields(page.Request.Form);
                    SafeDictionary<FieldDescriptor, string> controlsToValidate;
                    Packet packet;
                    try
                    {
                        packet = Save.CreatePacket(fields, out controlsToValidate);
                    }
                    catch (Exception ex)
                    {
                        SheerResponse.Alert(ex.Message, new string[0]);
                        return;
                    }
                    Item item = context.Items[0];
                    if (WebEditUtil.CanDesignItem(item))
                    {
                        Save.AddLayoutField(page, packet, item);
                    }
                    ValidatorsMode mode;
                    Sitecore.Data.Validators.ValidatorCollection validators = this.GetValidators(item, controlsToValidate, out mode);
                    string formValue = WebUtil.GetFormValue("scValidatorsKey");
                    if (!string.IsNullOrEmpty(formValue))
                    {
                        validators.Key = formValue;
                        ValidatorManager.SetValidators(mode, formValue, validators);
                    }
                    Pipeline pipeline = PipelineFactory.GetPipeline("saveUI");
                    pipeline.ID = ShortID.Encode(ID.NewID);
                    SaveArgs saveArgs = new SaveArgs(packet.XmlDocument)
                    {
                        SaveAnimation = false,
                        PostAction = StringUtil.GetString(new string[]
						{
							context.Parameters["postaction"]
						}),
                        PolicyBasedLocking = true
                    };
                    saveArgs.CustomData["showvalidationdetails"] = true;
                    SheerResponse.SetPipeline(pipeline.ID);
                    pipeline.Start(saveArgs);
                    if (!string.IsNullOrEmpty(saveArgs.Error))
                    {
                        SheerResponse.Alert(saveArgs.Error, new string[0]);
                    }
                }
            }
        }

        private Sitecore.Data.Validators.ValidatorCollection GetValidators(Item item, SafeDictionary<FieldDescriptor, string> controlsToValidate, out ValidatorsMode mode)
        {
            MethodInfo method = typeof(Sitecore.Shell.Applications.WebEdit.Commands.Save).GetMethod("GetValidators", BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[]
			{
				typeof(Item),
				typeof(SafeDictionary<FieldDescriptor, string>),
				typeof(ValidatorsMode).MakeByRefType()
			}, null);
            object[] array = new object[]
			{
				item,
				controlsToValidate,
				ValidatorsMode.Gutter
			};
            Sitecore.Data.Validators.ValidatorCollection result = (Sitecore.Data.Validators.ValidatorCollection)method.Invoke(this, array);
            mode = (ValidatorsMode)array[2];
            return result;
        }
    }
}