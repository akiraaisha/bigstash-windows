using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DeepfreezeModel
{
    public class Enumerations
    {
        public enum Status
        {
            [StringValue("pending")]
            Pending,

            [StringValue("uploaded")]
            Uploaded,

            [StringValue("error")]
            Error,

            [StringValue("completed")]
            Completed,

            [StringValue("paused")]
            Paused,

            [StringValue("uploading")]
            Uploading,

            [StringValue("not_found")]
            NotFound, // use this for cases when the upload is deleted from the server, but the client still has the local upload file.

            [StringValue("corrupted")]
            Corrupted
        }

        public static Status GetStatusFromString(string statusString)
        {
            Status status = Status.Error;

            switch(statusString)
            {
                case "uploaded":
                    status = Status.Uploaded;
                    break;
                case "completed":
                    status = Status.Completed;
                    break;
                case "paused":
                    status = Status.Paused;
                    break;
                case "error":
                    status = Status.Error;
                    break;
                case "pending":
                    status = Status.Paused;
                    break;
                case "not_found":
                    status = Status.NotFound;
                    break;
                case "corrupted":
                    status = Status.Corrupted;
                    break;
            }

            return status;
        }

        public enum UploadAction
        {
            Create,
            Start,
            Pause
        }

        public enum FileCategory
        {
            [StringValue("Normal")]
            Normal,

            [StringValue("Invalid characters in filename")]
            InvalidCharacterInName,

            [StringValue("File with metadata")]
            MetadataFile,

            [StringValue("Temporary file")]
            TemporaryFile,

            [StringValue("Trailing periods or whitespace in filename")]
            TrailingPeriodsOrWhiteSpaceInName,

            [StringValue("Ignored system file")]
            IgnoredSystemFile,

            [StringValue("Filename too long")]
            FileNameTooLong,

            [StringValue("Unsynced online file")]
            UnsyncedOnlineFile,

            [StringValue("Restricted directory")]
            RestrictedDirectory
        }
    }

    /// <summary>
    /// This class defines an attribute that represents the string value of an enum.
    /// It can be used in cases where an enumeration has different than int represation.
    /// </summary>
    public class StringValueAttribute : Attribute
    {
        public string StringValue { get; protected set; }

        public StringValueAttribute(string value)
        {
            this.StringValue = value;
        }
    }

    public static class EnumExtension
    {
        /// <summary>
        /// Gets the string representation of a given enum value. A StringValue has to 
        /// be assigned to the items of an enumeration in order for this method to work.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string GetStringValue(this Enum value)
        {
            // Get the type
            Type type = value.GetType();

            // Get fieldinfo for this type
            FieldInfo fieldInfo = type.GetField(value.ToString());

            // Get the stringvalue attributes
            StringValueAttribute[] attribs = fieldInfo.GetCustomAttributes(
                typeof(StringValueAttribute), false) as StringValueAttribute[];

            // Return the first if there was a match.
            return attribs.Length > 0 ? attribs[0].StringValue : null;
        }
    }
}
