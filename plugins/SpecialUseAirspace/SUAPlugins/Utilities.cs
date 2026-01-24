using Microsoft.Xrm.Sdk;
using System;

namespace SUAPlugins
{
    public static class Utilities
    {
        public static T GetValueOnUpdate<T>(Entity target, Entity preImage, string attribute)
        {
            if (target == null)
                throw new ArgumentNullException("target");
            if (preImage == null)
                throw new ArgumentNullException("preImage");

            if (!target.TryGetAttributeValue(attribute, out T value))
            {
                preImage.TryGetAttributeValue(attribute, out value);
            }
            return value;
        }

        public static int GetAliasedInt(Entity e, string aliasAttribute)
        {
            var av =
                e.GetAttributeValue<AliasedValue>(aliasAttribute)
                ?? throw new InvalidPluginExecutionException(
                    $"Missing aliased attribute {aliasAttribute}"
                );

            try
            {
                return Convert.ToInt32(av.Value);
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException(
                    $"{aliasAttribute} was not a valid integer",
                    ex
                );
            }
        }

        public static int? TryGetAliasedInt(Entity e, string aliasAttribute)
        {
            var av = e.GetAttributeValue<AliasedValue>(aliasAttribute);
            if (av?.Value == null)
                return null;

            try
            {
                return Convert.ToInt32(av.Value);
            }
            catch
            {
                return null;
            }
        }
    }
}
