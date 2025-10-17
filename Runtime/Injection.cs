using System;
using System.Collections.Generic;
using System.Reflection;

namespace Exerussus.GameSharing.Runtime
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public class InjectSharedObjectAttribute : Attribute
    {
        public Type MainType { get; }
        public Type SubType { get; }

        public InjectSharedObjectAttribute()
        {
        }

        public InjectSharedObjectAttribute(Type mainType)
        {
            MainType = mainType;
        }

        public InjectSharedObjectAttribute(Type mainType, Type subType)
        {
            MainType = mainType;
            SubType = subType;
        }
    }

    public static class SharedObjectInjector
    {
        public static void InjectSharedObjects(this GameShare gameShare, object injectingClass)
        {
            InjectSharedObjects(injectingClass, gameShare);
        }

        public static void InjectSharedObjects(object target, GameShare gameShare)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (gameShare == null)
                throw new ArgumentNullException(nameof(gameShare));

            var visited = new HashSet<Type>();
            InjectTypeHierarchy(target, gameShare, target.GetType(), visited);
        }

        private static void InjectTypeHierarchy(object target, GameShare gameShare, Type type, HashSet<Type> visited)
        {
            if (type == null || type == typeof(object) || !visited.Add(type))
                return;

            // 1. Инжектим в поля и свойства типа
            InjectMembers(target, gameShare, type);

            // 2. Рекурсивно обрабатываем базовый тип
            InjectTypeHierarchy(target, gameShare, type.BaseType, visited);

            // 3. И все интерфейсы, которые он реализует
            foreach (var iface in type.GetInterfaces()) InjectTypeHierarchy(target, gameShare, iface, visited);
        }

        private static void InjectMembers(object target, GameShare gameShare, Type type)
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

            var fields = type.GetFields(flags);
            var properties = type.GetProperties(flags);

            foreach (var field in fields)
            {
                ProcessInjection(field, target, gameShare,
                    f => f.FieldType,
                    sharedObject => field.SetValue(target, sharedObject));
            }

            foreach (var property in properties)
            {
                ProcessInjection(property, target, gameShare,
                    p => p.PropertyType,
                    sharedObject =>
                    {
                        if (property.SetMethod != null)
                        {
                            property.SetValue(target, sharedObject);
                        }
                        else
                        {
                            // Пытаемся достучаться до авто-бэкинг-филда
                            var backingFieldName = $"<{property.Name}>k__BackingField";
                            var backingField = type.GetField(backingFieldName, BindingFlags.NonPublic | BindingFlags.Instance);
                            if (backingField != null)
                            {
                                backingField.SetValue(target, sharedObject);
                            }
                        }
                    });
            }
        }

        private static void ProcessInjection<T>(
            T member,
            object target,
            GameShare gameShare,
            Func<T, Type> getType,
            Action<object> setValue) where T : MemberInfo
        {
#if UNITY_EDITOR
            try
            {
                var attribute = Attribute.GetCustomAttribute(member, typeof(InjectSharedObjectAttribute)) as InjectSharedObjectAttribute;
                if (attribute == null) return;

                var memberType = getType(member);
                var sharedObject = GetSharedObject(gameShare, memberType, attribute);
                setValue(sharedObject);
            }
            catch (Exception e)
            {
                var memberType = getType(member);
                throw new Exception(
                    $"Ошибка при инъекции в классе : {target.GetType().Name} | Поле: {member.Name} | Зависимость : {memberType}.\n\n{e.Message}\n{e.StackTrace}");
            }
#else
            var attribute = Attribute.GetCustomAttribute(member, typeof(InjectSharedObjectAttribute)) as InjectSharedObjectAttribute;
            if (attribute == null) return;

            var memberType = getType(member);
            var sharedObject = GetSharedObject(gameShare, memberType, attribute);
            setValue(sharedObject);
#endif
        }

        private static object GetSharedObject(GameShare gameShare, Type memberType, InjectSharedObjectAttribute attribute)
        {
            MethodInfo method;
            if (attribute.MainType == null)
            {
                method = typeof(GameShare).GetMethod("InjectSharedObject", BindingFlags.NonPublic | BindingFlags.Instance);
                var genericMethod = method.MakeGenericMethod(memberType);
                return genericMethod.Invoke(gameShare, new object[] { memberType });
            }

            if (attribute.SubType == null)
            {
                method = typeof(GameShare).GetMethod("InjectSharedObject", BindingFlags.NonPublic | BindingFlags.Instance);
                var genericMethod = method.MakeGenericMethod(memberType);
                return genericMethod.Invoke(gameShare, new object[] { attribute.MainType });
            }

            method = typeof(GameShare).GetMethod("InjectSharedSubTypeObject", BindingFlags.NonPublic | BindingFlags.Instance);
            var genericMethodWithSub = method.MakeGenericMethod(memberType);
            return genericMethodWithSub.Invoke(gameShare, new object[] { attribute.MainType, attribute.SubType });
        }
    }
}