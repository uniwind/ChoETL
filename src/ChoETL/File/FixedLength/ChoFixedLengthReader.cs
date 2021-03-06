﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Diagnostics;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ChoETL
{
    public class ChoFixedLengthReader<T> : ChoReader, IDisposable, IEnumerable<T>
        where T : class
    {
        private TextReader _textReader;
        private bool _closeStreamOnDispose = false;
        private Lazy<IEnumerator<T>> _enumerator = null;
        private CultureInfo _prevCultureInfo = null;
        private bool _clearFields = false;
        public TraceSwitch TraceSwitch = ChoETLFramework.TraceSwitch;
        public event EventHandler<ChoRowsLoadedEventArgs> RowsLoaded;
        public event EventHandler<ChoEventArgs<IDictionary<string, Type>>> MembersDiscovered;
        public event EventHandler<ChoMapColumnEventArgs> MapColumn;
        public event EventHandler<ChoEmptyLineEventArgs> EmptyLineFound;
        private bool _isDisposed = false;

        public override dynamic Context
        {
            get { return Configuration.Context; }
        }

        public ChoFixedLengthRecordConfiguration Configuration
        {
            get;
            private set;
        }

        public ChoFixedLengthReader(StringBuilder sb, ChoFixedLengthRecordConfiguration configuration = null) : this(new StringReader(sb.ToString()), configuration)
        {

        }

        public ChoFixedLengthReader(ChoFixedLengthRecordConfiguration configuration = null)
        {
            Configuration = configuration;
            Init();
        }

        public ChoFixedLengthReader(string filePath, ChoFixedLengthRecordConfiguration configuration = null)
        {
            ChoGuard.ArgumentNotNullOrEmpty(filePath, "FilePath");

            Configuration = configuration;

            Init();

            _textReader = new StreamReader(ChoPath.GetFullPath(filePath), Configuration.GetEncoding(filePath), false, Configuration.BufferSize);
            _closeStreamOnDispose = true;
        }

        public ChoFixedLengthReader(TextReader textReader, ChoFixedLengthRecordConfiguration configuration = null)
        {
            ChoGuard.ArgumentNotNull(textReader, "TextReader");

            Configuration = configuration;
            Init();

            _textReader = textReader;
        }

        public ChoFixedLengthReader(Stream inStream, ChoFixedLengthRecordConfiguration configuration = null)
        {
            ChoGuard.ArgumentNotNull(inStream, "Stream");

            Configuration = configuration;
            Init();
            if (inStream is MemoryStream)
                _textReader = new StreamReader(inStream);
            else
                _textReader = new StreamReader(inStream, Configuration.GetEncoding(inStream), false, Configuration.BufferSize);
            _closeStreamOnDispose = true;
        }

        public ChoFixedLengthReader<T> Load(string filePath)
        {
            ChoGuard.ArgumentNotNullOrEmpty(filePath, "FilePath");

            Close();
            Init();
            _textReader = new StreamReader(ChoPath.GetFullPath(filePath), Configuration.GetEncoding(filePath), false, Configuration.BufferSize);
            _closeStreamOnDispose = true;

            return this;
        }

        public ChoFixedLengthReader<T> Load(TextReader textReader)
        {
            ChoGuard.ArgumentNotNull(textReader, "TextReader");

            Close();
            Init();
            _textReader = textReader;
            _closeStreamOnDispose = false;

            return this;
        }

        public ChoFixedLengthReader<T> Load(Stream inStream)
        {
            ChoGuard.ArgumentNotNull(inStream, "Stream");

            Close();
            Init();
            if (inStream is MemoryStream)
                _textReader = new StreamReader(inStream);
            else
                _textReader = new StreamReader(inStream, Configuration.GetEncoding(inStream), false, Configuration.BufferSize);
            _closeStreamOnDispose = true;

            return this;
        }

        public void Close()
        {
            Dispose();
        }

        public T Read()
        {
            if (_enumerator.Value.MoveNext())
                return _enumerator.Value.Current;
            else
                return default(T);
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            if (_closeStreamOnDispose)
            {
                if (_textReader != null)
                {
                    _textReader.Dispose();
                    _textReader = null;
                }
            }

            if (!ChoETLFrxBootstrap.IsSandboxEnvironment)
                System.Threading.Thread.CurrentThread.CurrentCulture = _prevCultureInfo;

            _closeStreamOnDispose = false;
        }

        private void Init()
        {
            _enumerator = new Lazy<IEnumerator<T>>(() => GetEnumerator());
            if (Configuration == null)
                Configuration = new ChoFixedLengthRecordConfiguration(typeof(T));
            else
                Configuration.RecordType = typeof(T);

            Configuration.RecordType = ResolveRecordType(Configuration.RecordType);
            if (!ChoETLFrxBootstrap.IsSandboxEnvironment)
            {
                _prevCultureInfo = System.Threading.Thread.CurrentThread.CurrentCulture;
                System.Threading.Thread.CurrentThread.CurrentCulture = Configuration.Culture;
            }
        }

        public static ChoFixedLengthReader<T> LoadText(string inputText, Encoding encoding = null, ChoFixedLengthRecordConfiguration configuration = null, TraceSwitch traceSwitch = null)
        {
            var r = new ChoFixedLengthReader<T>(inputText.ToStream(encoding), configuration) { TraceSwitch = traceSwitch == null ? ChoETLFramework.TraceSwitch : traceSwitch };
            r._closeStreamOnDispose = true;

            return r;
        }

        public IEnumerable<T> DeserializeText(string inputText, Encoding encoding = null, ChoFixedLengthRecordConfiguration configuration = null, TraceSwitch traceSwitch = null)
        {
            if (configuration == null)
                configuration = Configuration;

            return new ChoFixedLengthReader<T>(inputText.ToStream(encoding), configuration) { TraceSwitch = traceSwitch == null ? ChoETLFramework.TraceSwitch : traceSwitch };
        }

        public IEnumerable<T> Deserialize(string filePath, ChoFixedLengthRecordConfiguration configuration = null, TraceSwitch traceSwitch = null)
        {
            if (configuration == null)
                configuration = Configuration;

            return new ChoFixedLengthReader<T>(filePath, configuration) { TraceSwitch = traceSwitch == null ? ChoETLFramework.TraceSwitch : traceSwitch };
        }

        public IEnumerable<T> Deserialize(TextReader textReader, ChoFixedLengthRecordConfiguration configuration = null, TraceSwitch traceSwitch = null)
        {
            if (configuration == null)
                configuration = Configuration;

            return new ChoFixedLengthReader<T>(textReader, configuration) { TraceSwitch = traceSwitch == null ? ChoETLFramework.TraceSwitch : traceSwitch };
        }

        public IEnumerable<T> Deserialize(Stream inStream, ChoFixedLengthRecordConfiguration configuration = null, TraceSwitch traceSwitch = null)
        {
            if (configuration == null)
                configuration = Configuration;

            return new ChoFixedLengthReader<T>(inStream, configuration) { TraceSwitch = traceSwitch == null ? ChoETLFramework.TraceSwitch : traceSwitch };
        }

        internal static IEnumerator<object> LoadText(Type recType, string inputText, ChoFixedLengthRecordConfiguration configuration, Encoding encoding, int bufferSize, TraceSwitch traceSwitch = null)
        {
            ChoFixedLengthRecordReader rr = new ChoFixedLengthRecordReader(recType, configuration);
            rr.TraceSwitch = traceSwitch == null ? ChoETLFramework.TraceSwitchOff : traceSwitch;
            return rr.AsEnumerable(new StreamReader(inputText.ToStream(), encoding, false, bufferSize)).GetEnumerator();
        }

        public IEnumerator<T> GetEnumerator()
        {
            ChoFixedLengthRecordReader rr = new ChoFixedLengthRecordReader(typeof(T), Configuration);
            rr.Reader = this;
            rr.TraceSwitch = TraceSwitch;
            rr.RowsLoaded += NotifyRowsLoaded;
            rr.MembersDiscovered += MembersDiscovered;
            var e = rr.AsEnumerable(_textReader).GetEnumerator();
            return ChoEnumeratorWrapper.BuildEnumerable<T>(() => e.MoveNext(), () => (T)ChoConvert.ChangeType<ChoRecordFieldAttribute>(e.Current, typeof(T))).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IDataReader AsDataReader()
        {
            ChoFixedLengthRecordReader rr = new ChoFixedLengthRecordReader(typeof(T), Configuration);
            rr.Reader = this;
            rr.TraceSwitch = TraceSwitch;
            rr.RowsLoaded += NotifyRowsLoaded;
            rr.MembersDiscovered += MembersDiscovered;
            var dr = new ChoEnumerableDataReader(rr.AsEnumerable(_textReader), rr);
            return dr;
        }

        public DataTable AsDataTable(string tableName = null)
        {
            DataTable dt = tableName.IsNullOrWhiteSpace() ? new DataTable() : new DataTable(tableName);
            dt.Load(AsDataReader());
            return dt;
        }

        public int Fill(DataTable dt)
        {
            if (dt == null)
                throw new ArgumentException("Missing datatable.");
            dt.Load(AsDataReader());

            return dt.Rows.Count;
        }

        private void NotifyRowsLoaded(object sender, ChoRowsLoadedEventArgs e)
        {
            EventHandler<ChoRowsLoadedEventArgs> rowsLoadedEvent = RowsLoaded;
            if (rowsLoadedEvent == null)
            {
                if (!e.IsFinal)
                    ChoETLLog.Info(e.RowsLoaded.ToString("#,##0") + " records loaded.");
                else
                    ChoETLLog.Info("Total " + e.RowsLoaded.ToString("#,##0") + " records loaded.");
            }
            else
                rowsLoadedEvent(this, e);
        }

        public override bool RaiseMapColumn(int colPos, string colName, out string newColName)
        {
            newColName = null;
            EventHandler<ChoMapColumnEventArgs> mapColumn = MapColumn;
            if (mapColumn == null)
            {
                var fc = Configuration.FixedLengthRecordFieldConfigurations.Where(c => c.AltFieldNamesArray.Contains(colName)).FirstOrDefault();
                if (fc != null)
                {
                    newColName = fc.FieldName;
                    return true;
                }
                return false;
            }

            var ea = new ChoMapColumnEventArgs(colPos, colName);
            mapColumn(this, ea);
            if (ea.Resolved)
                newColName = ea.NewColName;

            return ea.Resolved;
        }

        public override bool RaiseReportEmptyLine(long lineNo)
        {
            EventHandler<ChoEmptyLineEventArgs> emptyLineFound = EmptyLineFound;
            if (emptyLineFound == null)
            {
                return true;
            }

            var ea = new ChoEmptyLineEventArgs(lineNo);
            emptyLineFound(this, ea);
            return ea.Continue;
        }
        
        public override bool TryValidate(object target, ICollection<ValidationResult> validationResults)
        {
            ChoObjectValidationMode prevObjValidationMode = Configuration.ObjectValidationMode;

            if (Configuration.ObjectValidationMode == ChoObjectValidationMode.Off)
                Configuration.ObjectValidationMode = ChoObjectValidationMode.ObjectLevel;

            try
            {
                T rec = default(T);
                while ((rec = Read()) != null)
                {

                }
                return IsValid;
            }
            finally
            {
                Configuration.ObjectValidationMode = prevObjValidationMode;
            }
        }

        #region Fluent API

        public ChoFixedLengthReader<T> NotifyAfter(long rowsLoaded)
        {
            Configuration.NotifyAfter = rowsLoaded;
            return this;
        }

        public ChoFixedLengthReader<T> WithRecordLength(int length)
        {
            Configuration.RecordLength = length;
            return this;
        }

        public ChoFixedLengthReader<T> WithEOLDelimiter(string delimiter)
        {
            Configuration.EOLDelimiter = delimiter;
            return this;
        }

		public ChoFixedLengthReader<T> IgnoreHeader()
		{
			Configuration.FileHeaderConfiguration.HasHeaderRecord = true;
			Configuration.FileHeaderConfiguration.IgnoreHeader = true;

			return this;
		}

		public ChoFixedLengthReader<T> WithFirstLineHeader(bool ignoreHeader = false)
        {
            Configuration.FileHeaderConfiguration.HasHeaderRecord = true;
            Configuration.FileHeaderConfiguration.IgnoreHeader = ignoreHeader;
            return this;
        }
        public ChoFixedLengthReader<T> WithHeaderLineAt(int pos = 1, bool ignoreHeader = false)
        {
            Configuration.FileHeaderConfiguration.HeaderLineAt = pos;
            Configuration.FileHeaderConfiguration.HasHeaderRecord = true;
            Configuration.FileHeaderConfiguration.IgnoreHeader = ignoreHeader;

            return this;
        }

        public ChoFixedLengthReader<T> QuoteAllFields(bool flag = true, char quoteChar = '"')
        {
            Configuration.QuoteAllFields = flag;
            Configuration.QuoteChar = quoteChar;
            return this;
        }

		public ChoFixedLengthReader<T> ClearFields()
		{
			Configuration.FixedLengthRecordFieldConfigurations.Clear();
			_clearFields = true;
			return this;
		}

		public ChoFixedLengthReader<T> IgnoreField<TField>(Expression<Func<T, TField>> field)
		{
			if (field != null)
				return IgnoreField(field.GetFullyQualifiedMemberName());
			else
				return this;
		}

		public ChoFixedLengthReader<T> IgnoreField(string fieldName)
        {
            if (!fieldName.IsNullOrWhiteSpace())
            {
                string fnTrim = null;
                if (!_clearFields)
                {
					ClearFields();
					Configuration.MapRecordFields(Configuration.RecordType);
                }
                fnTrim = fieldName.NTrim();
                if (Configuration.FixedLengthRecordFieldConfigurations.Any(o => o.Name == fnTrim))
                    Configuration.FixedLengthRecordFieldConfigurations.Remove(Configuration.FixedLengthRecordFieldConfigurations.Where(o => o.Name == fnTrim).First());
            }

            return this;
        }

		public ChoFixedLengthReader<T> WithField<TField>(Expression<Func<T, TField>> field, int startIndex, int size, Type fieldType = null, bool? quoteField = null, ChoFieldValueTrimOption? fieldValueTrimOption = null,
			string fieldName = null, Func<object, object> valueConverter = null, object defaultValue = null, object fallbackValue = null, string altFieldNames = null, string formatText = null)
		{
			if (field == null)
				return this;

			return WithField(field.GetMemberName(), startIndex, size, fieldType, quoteField, fieldValueTrimOption,
				fieldName, valueConverter, defaultValue, fallbackValue, altFieldNames, field.GetFullyQualifiedMemberName(), formatText);
		}

		public ChoFixedLengthReader<T> WithField(string name, int startIndex, int size, Type fieldType = null, bool? quoteField = null, ChoFieldValueTrimOption? fieldValueTrimOption = null,
			string fieldName = null, Func<object, object> valueConverter = null, object defaultValue = null, object fallbackValue = null, string altFieldNames = null, string formatText = null)
		{
			return WithField(name, startIndex, size, fieldType, quoteField, fieldValueTrimOption,
				fieldName, valueConverter, defaultValue, fallbackValue, altFieldNames, null, formatText);
		}

		private ChoFixedLengthReader<T> WithField(string name, int startIndex, int size, Type fieldType = null, bool? quoteField = null, ChoFieldValueTrimOption? fieldValueTrimOption = null,
            string fieldName = null, Func<object, object> valueConverter = null, object defaultValue = null, object fallbackValue = null, string altFieldNames = null,
			string fullyQualifiedMemberName = null, string formatText = null)
        {
            if (!name.IsNullOrEmpty())
            {
                if (!_clearFields)
                {
					ClearFields();
					Configuration.MapRecordFields(Configuration.RecordType);
                }
                if (fieldName.IsNullOrWhiteSpace())
                    fieldName = name;

                string fnTrim = name.NTrim();
                ChoFixedLengthRecordFieldConfiguration fc = null;
				PropertyDescriptor pd = null;
				if (Configuration.FixedLengthRecordFieldConfigurations.Any(o => o.Name == fnTrim))
				{
					fc = Configuration.FixedLengthRecordFieldConfigurations.Where(o => o.Name == fnTrim).First();
					Configuration.FixedLengthRecordFieldConfigurations.Remove(fc);
				}
				else
					pd = ChoTypeDescriptor.GetNestedProperty(typeof(T), fullyQualifiedMemberName.IsNullOrWhiteSpace() ? name : fullyQualifiedMemberName);

				var nfc = new ChoFixedLengthRecordFieldConfiguration(fnTrim, startIndex, size)
				{
					FieldType = fieldType,
					QuoteField = quoteField,
					FieldValueTrimOption = fieldValueTrimOption,
					FieldName = fieldName,
					ValueConverter = valueConverter,
					DefaultValue = defaultValue,
					FallbackValue = fallbackValue,
					AltFieldNames = altFieldNames,
                    FormatText = formatText
				};
				if (fullyQualifiedMemberName.IsNullOrWhiteSpace())
				{
					nfc.PropertyDescriptor = fc != null ? fc.PropertyDescriptor : pd;
					nfc.DeclaringMember = fc != null ? fc.DeclaringMember : fullyQualifiedMemberName;
				}
				else
				{
					pd = ChoTypeDescriptor.GetNestedProperty(typeof(T), fullyQualifiedMemberName);
					nfc.PropertyDescriptor = pd;
					nfc.DeclaringMember = fullyQualifiedMemberName;
				}
				if (pd != null)
				{
					if (nfc.FieldType == null)
						nfc.FieldType = pd.PropertyType;
				}

				Configuration.FixedLengthRecordFieldConfigurations.Add(nfc);
            }

            return this;
        }

        public ChoFixedLengthReader<T> ColumnCountStrict()
        {
            Configuration.ColumnCountStrict = true;
            return this;
        }

        public ChoFixedLengthReader<T> ColumnOrderStrict()
        {
            Configuration.ColumnOrderStrict = true;
            return this;
        }

        public ChoFixedLengthReader<T> Configure(Action<ChoFixedLengthRecordConfiguration> action)
        {
            if (action != null)
                action(Configuration);

            return this;
        }
        public ChoFixedLengthReader<T> Setup(Action<ChoFixedLengthReader<T>> action)
        {
            if (action != null)
                action(this);

            return this;
        }

        public ChoFixedLengthReader<T> MapRecordFields<T1>()
        {
            MapRecordFields(typeof(T1));
            return this;
        }

        //public ChoFixedLengthReader<T> MapRecordFields(Type recordType)
        //{
        //    if (recordType != null)
        //    {
        //        if (recordType != null && !typeof(T).IsAssignableFrom(recordType))
        //            throw new ChoParserException("Incompatible [{0}] record type passed.".FormatString(recordType.FullName));
        //        Configuration.MapRecordFields(recordType);
        //    }

        //    return this;
        //}

		public ChoFixedLengthReader<T> MapRecordFields(params Type[] recordTypes)
		{
			Configuration.RecordTypeMapped = true;
			if (recordTypes != null)
			{
				foreach (var t in recordTypes)
				{
					if (t == null)
						continue;

					//if (!typeof(T).IsAssignableFrom(t))
					//	throw new ChoParserException("Incompatible [{0}] record type passed.".FormatString(t.FullName));

					Configuration.RecordTypeConfiguration.RegisterType(t);
				}
			}

			Configuration.MapRecordFields(recordTypes);
			return this;
		}
		public ChoFixedLengthReader<T> WithCustomRecordTypeCodeExtractor(Func<string, string> recordTypeCodeExtractor)
        {
			Configuration.SupportsMultiRecordTypes = true;
			Configuration.RecordTypeCodeExtractor = recordTypeCodeExtractor;
            return this;
        }

        public ChoFixedLengthReader<T> WithCustomRecordSelector(Func<object, Type> recordSelector)
        {
			Configuration.SupportsMultiRecordTypes = true;
			Configuration.RecordSelector = recordSelector;
            return this;
        }

        public ChoFixedLengthReader<T> WithRecordSelector(int startIndex, int size, Type defaultRecordType = null, params Type[] recordTypes)
        {
			Configuration.SupportsMultiRecordTypes = true;

			Configuration.RecordTypeConfiguration.StartIndex = startIndex;
            Configuration.RecordTypeConfiguration.Size = size;
            if (defaultRecordType != null && !typeof(T).IsAssignableFrom(defaultRecordType))
                throw new ChoParserException("Incompatible [{0}] record type passed.".FormatString(defaultRecordType.FullName));
            Configuration.RecordTypeConfiguration.DefaultRecordType = defaultRecordType;

            if (recordTypes != null)
            {
                foreach (var t in recordTypes)
                {
                    if (t == null)
                        continue;

                    //if (!typeof(T).IsAssignableFrom(t))
                    //    throw new ChoParserException("Incompatible [{0}] record type passed.".FormatString(t.FullName));

                    Configuration.RecordTypeConfiguration.RegisterType(t);
                }
            }

			//Configuration.RecordTypeMapped = true;
			//Configuration.MapRecordFields(ChoArray.Combine<Type>(new Type[] { defaultRecordType }, recordTypes));
            return this;
        }

        #endregion Fluent API
   
		~ChoFixedLengthReader()
		{
			Dispose();
		}
 }

    public class ChoFixedLengthReader : ChoFixedLengthReader<dynamic>
    {
        public ChoFixedLengthReader(StringBuilder sb, ChoFixedLengthRecordConfiguration configuration = null) : base(sb, configuration)
        {

        }

        public ChoFixedLengthReader(ChoFixedLengthRecordConfiguration configuration = null)
           : base(configuration)
        {

        }
        public ChoFixedLengthReader(string filePath, ChoFixedLengthRecordConfiguration configuration = null)
            : base(filePath, configuration)
        {

        }
        public ChoFixedLengthReader(TextReader textReader, ChoFixedLengthRecordConfiguration configuration = null)
            : base(textReader, configuration)
        {
        }
        public ChoFixedLengthReader(Stream inStream, ChoFixedLengthRecordConfiguration configuration = null)
            : base(inStream, configuration)
        {
        }
    }
}
