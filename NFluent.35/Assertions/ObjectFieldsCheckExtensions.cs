﻿namespace NFluent
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Text.RegularExpressions;

    using NFluent.Extensibility;
    using NFluent.Extensions;
    using NFluent.Helpers;

    /// <summary>
    /// Provides check methods to be executed on an object instance.
    /// </summary>
    public static class ObjectFieldsCheckExtensions
    {
        #region fields

        private static readonly Regex AutoPropertyMask;

        private static readonly Regex AnonymousTypeFieldMask;

        private static readonly Regex MonoAnonymousTypeFieldMask;

        #endregion

        static ObjectFieldsCheckExtensions()
        {
            AutoPropertyMask = new Regex("^<(.*)>k_");
            AnonymousTypeFieldMask = new Regex("^<(.*)>i_");
            MonoAnonymousTypeFieldMask = new Regex("^<(.*)>\\z");
        }

        /// <summary>
        /// Kind of field (whether normal, generated by an auto-property, an anonymous class, etc.
        /// </summary>
        public enum FieldKind
        {
            /// <summary>
            /// Normal field.
            /// </summary>
            Normal,

            /// <summary>
            /// Field generated by an auto-property.
            /// </summary>
            AutoProperty,

            /// <summary>
            /// Field generated by an anonymous class.
            /// </summary>
            AnonymousClass
        }

        /// <summary>
        /// Checks that the actual value has fields equals to the expected value ones.
        /// </summary>
        /// <typeparam name="T">
        /// Type of the checked value.
        /// </typeparam>
        /// <param name="check">The fluent check to be extended.</param>
        /// <param name="expected">The expected value.</param>
        /// <returns>
        /// A check link.
        /// </returns>
        /// <exception cref="FluentCheckException">The actual value doesn't have all fields equal to the expected value ones.</exception>
        /// <remarks>
        /// The comparison is done field by field.
        /// </remarks>
        public static ICheckLink<ICheck<T>> HasFieldsWithSameValues<T>(this ICheck<T> check, object expected)
        {
            var checker = ExtensibilityHelper.ExtractChecker(check);
            var message = CheckFieldEquality(checker, checker.Value, expected, checker.Negated);

            if (message != null)
            {
                throw new FluentCheckException(message);
            }

            return checker.BuildChainingObject();
        }

        /// <summary>
        /// Checks that the actual value has fields equals to the expected value ones.
        /// </summary>
        /// <param name="check">The fluent check to be extended.</param>
        /// <param name="expected">The expected value.</param>
        /// <returns>
        /// A check link.
        /// </returns>
        /// <exception cref="FluentCheckException">The actual value doesn't have all fields equal to the expected value ones.</exception>
        /// <remarks>The comparison is done field by field.</remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use HasFieldsWithSameValues instead.")]
        public static ICheckLink<ICheck<object>> HasFieldsEqualToThose(this ICheck<object> check, object expected)
        {
            return HasFieldsWithSameValues(check, expected);
        }

        /// <summary>
        /// Checks that the actual value doesn't have all fields equal to the expected value ones.
        /// </summary>
        /// <typeparam name="T">
        /// Type of the checked value.
        /// </typeparam>
        /// <param name="check">The fluent check to be extended.</param>
        /// <param name="expected">The expected value.</param>
        /// <returns>
        /// A check link.
        /// </returns>
        /// <exception cref="FluentCheckException">The actual value has all fields equal to the expected value ones.</exception>
        /// <remarks>
        /// The comparison is done field by field.
        /// </remarks>
        public static ICheckLink<ICheck<T>> HasNotFieldsWithSameValues<T>(this ICheck<T> check, object expected)
        {
            var checker = ExtensibilityHelper.ExtractChecker(check);
            var negated = !checker.Negated;

            var message = CheckFieldEquality(checker, checker.Value, expected, negated);

            if (message != null)
            {
                throw new FluentCheckException(message);
            }

            return checker.BuildChainingObject();
        }

        /// <summary>
        /// Checks that the actual value doesn't have all fields equal to the expected value ones.
        /// </summary>
        /// <param name="check">The fluent check to be extended.</param>
        /// <param name="expected">The expected value.</param>
        /// <returns>
        /// A check link.
        /// </returns>
        /// <exception cref="FluentCheckException">The actual value has all fields equal to the expected value ones.</exception>
        /// <remarks>The comparison is done field by field.</remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use HasNotFieldsWithSameValues instead.")]
        public static ICheckLink<ICheck<object>> HasFieldsNotEqualToThose(this ICheck<object> check, object expected)
        {
            return HasNotFieldsWithSameValues(check, expected);
        }

        // Checks
        private static string CheckFieldEquality<T>(IChecker<T, ICheck<T>> checker, object value, object expected, bool negated, string prefix = "")
        {
            var invalidFields = new StringBuilder();

            // REFACTOR: this method which has too much lines
            string message = null;

            foreach (var fieldInfo in expected.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                // check for auto properties
                string fieldLabel;
                var expectedFieldsName = BuildFieldDescription(prefix, fieldInfo, out fieldLabel);
                var matchingActualField = FindField(value.GetType(), fieldInfo.Name);

                if (matchingActualField == null)
                {
                    // fields does not exist
                    if (!negated)
                    {
                        message = checker.BuildMessage(string.Format("The {{0}}'s {0} is absent from the {{1}}.", fieldLabel.DoubleCurlyBraces()))
                            .On(value)
                            .And.Expected(expected)
                            .ToString();
                    }

                    break;
                }

                // compare value
                var actualFieldValue = matchingActualField.GetValue(value);
                var expectedFieldValue = fieldInfo.GetValue(expected);

                if (expectedFieldValue == null)
                {
                    if ((actualFieldValue == null) != negated)
                    {
                        continue;
                    }

                    if (!negated)
                    {
                        message = checker.BuildMessage(string.Format("The {{0}}'s {0} does not have the expected value.", fieldLabel.DoubleCurlyBraces()))
                            .On(actualFieldValue)
                            .And.Expected(null)
                            .ToString();
                    }
                    else
                    {
                        message = checker.BuildMessage(string.Format("The {{0}}'s {0} has the same value in the comparand, whereas it must not.", fieldLabel.DoubleCurlyBraces()))
                            .On(null)
                            .And.Expected(null)
                            .Comparison("different from")
                            .ToString();
                    }

                    break;
                }

                // determines how comparison will be made
                if (!matchingActualField.FieldType.ImplementsEquals())
                {
                    // we recurse
                    message = CheckFieldEquality(checker, actualFieldValue, expectedFieldValue, negated, string.Format("{0}.", expectedFieldsName));
                    if (!string.IsNullOrEmpty(message))
                    {
                        invalidFields.AppendLine(message);
                    }
                }
                else if (expectedFieldValue.Equals(actualFieldValue) == negated)
                {
                    if (!negated)
                    {
                        var msg = 
                            checker.BuildMessage(
                                string.Format(
                                    "The {{0}}'s {0} does not have the expected value.",
                                    fieldLabel.DoubleCurlyBraces()));
                        EqualityHelper.FillEqualityErrorMessage(msg, actualFieldValue, expectedFieldValue);
                        message = msg.ToString();
                    }
                    else
                    {
                        message = checker.BuildMessage(string.Format("The {{0}}'s {0} has the same value in the comparand, whereas it must not.", fieldLabel.DoubleCurlyBraces()))
                            .On(actualFieldValue)
                            .And.Expected(expectedFieldValue)
                            .Comparison("different from")
                            .ToString();
                    }

                    invalidFields.AppendLine(message);
                    break;
                }
            }

            return message;
        }

        private static IEnumerable<FieldMatch> ScanFields(object value, object expected, string prefix = null)
        {
            var result = new List<FieldMatch>();
            foreach (var fieldInfo in expected.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                var expectedFieldDescription = new ExtendedFieldInfo(prefix, fieldInfo);
                var actualFieldMatching = FindField(value.GetType(), expectedFieldDescription.NameInSource);

                // field not found in SUT
                if (actualFieldMatching == null)
                {
                    result.Add(new FieldMatch(expectedFieldDescription, null));
                    continue;
                }

                var actualFieldDescription = new ExtendedFieldInfo(prefix, actualFieldMatching);
                
                // now, let's get to the values
                expectedFieldDescription.CaptureFieldValue(expected);
                actualFieldDescription.CaptureFieldValue(value);

                if (expectedFieldDescription.Value == null)
                {
                    if (actualFieldDescription.Value != null)
                    {
                        result.Add(new FieldMatch(expectedFieldDescription, actualFieldDescription));
                    }

                    continue;
                }

                if (expectedFieldDescription.ChecksIfImplementsEqual())
                {
                    result.AddRange(
                        ScanFields(
                            actualFieldDescription.Value,
                            expectedFieldDescription.Value,
                            string.Format("{0}.", expectedFieldDescription.LongFieldName)));
                }
                else
                {
                    if (!expectedFieldDescription.Value.Equals(actualFieldDescription.Value))
                    {
                        result.Add(new FieldMatch(expectedFieldDescription, actualFieldDescription));
                    }
                }
            }

            return result;
        }

        // assess if the name matches the given regexp and return the extracted if relevant.
        private static bool EvaluateCriteria(Regex expression, string name, out string actualFieldName)
        {
            var regTest = expression.Match(name);
            if (regTest.Groups.Count == 2)
            {
                actualFieldName = name.Substring(regTest.Groups[1].Index, regTest.Groups[1].Length);
                return true;
            }

            actualFieldName = string.Empty;
            return false;
        }

        // rebuild the field name as entered in the source code 
        internal static string ExtractFieldNameAsInSourceCode(string name, out FieldKind kind)
        {
            string result;
            if (EvaluateCriteria(AutoPropertyMask, name, out result))
            {
                kind = FieldKind.AutoProperty;
                return result;
            }

            if (EvaluateCriteria(AnonymousTypeFieldMask, name, out result))
            {
                kind = FieldKind.AnonymousClass;
                return result;
            }

            if (EvaluateCriteria(MonoAnonymousTypeFieldMask, name, out result))
            {
                kind = FieldKind.AnonymousClass;
                return result;
            }

            result = name;
            kind = FieldKind.Normal;
            return result;
        }

        private static string BuildFieldDescription(string prefix, FieldInfo fieldInfo, out string fieldLabel)
        {
            FieldKind actualFieldKind;

            var trueName = ExtractFieldNameAsInSourceCode(fieldInfo.Name, out actualFieldKind);
            var fieldname = string.Format("{0}{1}", prefix, trueName);
            switch (actualFieldKind)
            {
                case FieldKind.AnonymousClass:
                    fieldLabel = string.Format("field '{0}'", fieldname);
                    break;
                case FieldKind.AutoProperty:
                    fieldLabel = string.Format("autoproperty '{0}' (field '{1}')", fieldname, fieldInfo.Name);
                    break;
                default:
                    fieldLabel = string.Format("field '{0}'", fieldname);
                    break;
            }

            return fieldname;
        }

        private static FieldInfo FindField(Type type, string name)
        {
            while (type != null)
            {
                const BindingFlags BindingFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                var result = type.GetField(name, BindingFlags);

                if (result != null)
                {
                    return result;
                }

                if (type.BaseType == null)
                {
                    return null;
                }

                // compensate any autogenerated name
                FieldKind fieldKind;
                var actualName = ExtractFieldNameAsInSourceCode(name, out fieldKind);
                foreach (var field in from field in type.GetFields(BindingFlags) let fieldName = ExtractFieldNameAsInSourceCode(field.Name, out fieldKind) where fieldName == actualName select field)
                {
                    return field;
                }

                type = type.BaseType;
            }

            return null;
        }


        /// <summary>
        /// 
        /// </summary>
        public class ExtendedFieldInfo
        {
            /// <summary>
            /// 
            /// </summary>
            public string Prefix;

            /// <summary>
            /// 
            /// </summary>
            public FieldInfo Info;

            /// <summary>
            /// 
            /// </summary>
            public string NameInSource;

            /// <summary>
            /// 
            /// </summary>
            public FieldKind Kind;

            /// <summary>
            /// 
            /// </summary>
            public object Value;

            /// <summary>
            /// Initializes a new instance of the <see cref="ExtendedFieldInfo"/> class.
            /// </summary>
            /// <param name="prefix">
            /// The prefix (class path)
            /// </param>
            /// <param name="info">
            /// The field info.
            /// </param>
            public ExtendedFieldInfo(string prefix, FieldInfo info)
            {
                this.Prefix = prefix;
                this.Info = info;
                if (EvaluateCriteria(AutoPropertyMask, info.Name, out this.NameInSource))
                {
                    this.Kind = FieldKind.AutoProperty;
                }
                else if (EvaluateCriteria(AnonymousTypeFieldMask, info.Name, out this.NameInSource))
                {
                    this.Kind = FieldKind.AnonymousClass;
                }
                else if (EvaluateCriteria(MonoAnonymousTypeFieldMask, info.Name, out this.NameInSource))
                {
                    this.Kind = FieldKind.AnonymousClass;
                }
                else
                {
                    this.NameInSource = info.Name;
                    this.Kind = FieldKind.Normal;
                }
            }

            /// <summary>
            /// 
            /// </summary>
            public string LongFieldName
            {
                get
                {
                    return this.Prefix == null ? this.NameInSource : string.Format("{0}.{1}", this.Prefix, this.NameInSource);
                }
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="obj"></param>
            public void CaptureFieldValue(object obj)
            {
                this.Value = this.Info.GetValue(obj);
            }

            /// <summary>
            /// 
            /// </summary>
            /// <returns></returns>
            public bool ChecksIfImplementsEqual()
            {
                return this.Info.FieldType.ImplementsEquals();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public class FieldMatch
        {
            /// <summary>
            /// 
            /// </summary>
            public ExtendedFieldInfo Actual;
            /// <summary>
            /// 
            /// </summary>
            public ExtendedFieldInfo Expected;

            /// <summary>
            /// Initializes a new instance of the <see cref="T:System.Object"/> class.
            /// </summary>
            public FieldMatch(ExtendedFieldInfo actual, ExtendedFieldInfo expected)
            {
                this.Actual = actual;
                this.Expected = expected;
            }
        }

    }
}