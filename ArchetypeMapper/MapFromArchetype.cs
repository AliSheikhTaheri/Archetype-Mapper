namespace Zone.ArchetypeMapper
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Web.Script.Serialization;

    using Archetype.Models;

    using Zone.UmbracoMapper;

    [AttributeUsage(AttributeTargets.Property)]
    public class MapFromArchetypeAttribute : Attribute, IMapFromAttribute
    {
        #region Methods

        public void SetPropertyValue<T>(object fromObject, PropertyInfo property, T model, IUmbracoMapper mapper)
        {
            var fieldsets = GetArchetypeModel(fromObject);

            if (fieldsets != null)
            {
                var type = property.PropertyType.GetGenericArguments()[0];
                var method = GetType().GetMethod("GetItems", BindingFlags.NonPublic | BindingFlags.Instance);
                var genericMethod = method.MakeGenericMethod(type);
                var items = genericMethod.Invoke(this, new object[] { fieldsets, mapper });
                property.SetValue(model, items);
            }
        } 

        #endregion

        #region Helpers
        
        private IEnumerable<T> GetItems<T>(IEnumerable<ArchetypeFieldsetModel> fieldsets, IUmbracoMapper mapper)
        {
            foreach (var fieldset in fieldsets)
            {
                // Instantiate the T
                var instance = (T)Activator.CreateInstance(typeof(T));

                // make a dictionary of property alias and value
                var dictionary = fieldset.Properties.ToDictionary(property => FirstToUpper(property.Alias), property => property.Value);

                // If fieldset name is the same as instance type then lets map it to the instance
                if (instance.GetType().Name.ToLower() == fieldset.Alias.ToLower())
                {
                    mapper.Map(dictionary, (object)instance);
                }
                else // if not then lets find a property with the same name as fieldset name
                {
                    var property = instance.GetType().GetProperties().FirstOrDefault(x => x.Name.ToLower() == fieldset.Alias.ToLower());
                    if (property != null)
                    {
                        var propertyClass = Activator.CreateInstance(property.PropertyType);
                        mapper.Map(dictionary, propertyClass);

                        var propertyInfo = instance.GetType().GetProperty(property.Name);
                        propertyInfo.SetValue(instance, Convert.ChangeType(propertyClass, propertyInfo.PropertyType));
                    }
                }

                yield return instance;
            }
        }

        private string FirstToUpper(string s)
        {
            // This is required - otherwise mapper won't map from alias (xxxYyy) onto property name (XxxYyy)
            return string.IsNullOrEmpty(s) ? string.Empty : s.Substring(0, 1).ToUpper() + s.Substring(1);
        }

        public IEnumerable<ArchetypeFieldsetModel> GetArchetypeModel(object fromObject)
        {
            // Depending on how nested the archetype we're currently mapping from is,
            // fromObject can arrive in one of several different forms - we need to check
            // for all of these
            var json = fromObject as string;
            if (json != null)
            {
                return new JavaScriptSerializer().Deserialize<ArchetypeModel>(json);
            }

            var fieldsets = fromObject as IEnumerable<ArchetypeFieldsetModel>;
            if (fieldsets != null)
            {
                return fieldsets;
            }

            var properties = fromObject as IEnumerable<ArchetypePropertyModel>;
            if (properties != null)
            {
                var property = properties.FirstOrDefault();
                if (property != null)
                {
                    var innerJson = property.Value.ToString();
                    return new JavaScriptSerializer().Deserialize<ArchetypeModel>(innerJson);
                }
            }

            return null;
        }

        #endregion
    }
}
