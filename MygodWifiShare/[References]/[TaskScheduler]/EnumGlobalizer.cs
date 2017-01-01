﻿using System;
using System.Collections.Generic;
using System.Globalization;
using Mygod.WifiShare;

namespace Microsoft.Win32.TaskScheduler
{
	/// <summary>
	/// Functions to provide localized strings for enumerated types and values.
	/// </summary>
	public static class TaskEnumGlobalizer
	{
		/// <summary>
		/// Gets a string representing the localized value of the provided enum.
		/// </summary>
		/// <param name="enumValue">The enum value.</param>
		/// <returns>A localized string, if available.</returns>
		public static string GetString(object enumValue)
		{
			switch (enumValue.GetType().Name)
			{
				case "DaysOfTheWeek":
					return GetCultureEquivalentString((DaysOfTheWeek)enumValue);
				case "MonthsOfTheYear":
					return GetCultureEquivalentString((MonthsOfTheYear)enumValue);
				case "TaskTriggerType":
					return BuildEnumString("TriggerType", enumValue);
				case "WhichWeek":
					return BuildEnumString("WW", enumValue);
				case "TaskActionType":
					return BuildEnumString("ActionType", enumValue);
				case "TaskState":
					return BuildEnumString("TaskState", enumValue);
				default:
					break;
			}
			return enumValue.ToString();
		}

		private static string GetCultureEquivalentString(DaysOfTheWeek val)
		{
			if (val == DaysOfTheWeek.AllDays)
				return Resources.DOWAllDays;

			List<string> s = new List<string>(7);
			Array vals = Enum.GetValues(val.GetType());
			for (int i = 0; i < vals.Length - 1; i++)
			{
				if ((val & (DaysOfTheWeek)vals.GetValue(i)) > 0)
					s.Add(DateTimeFormatInfo.CurrentInfo.GetDayName((DayOfWeek)i));
			}

			return string.Join(Resources.ListSeparator, s.ToArray());
		}

		private static string GetCultureEquivalentString(MonthsOfTheYear val)
		{
			if (val == MonthsOfTheYear.AllMonths)
				return Resources.MOYAllMonths;

			List<string> s = new List<string>(12);
			Array vals = Enum.GetValues(val.GetType());
			for (int i = 0; i < vals.Length - 1; i++)
			{
				if ((val & (MonthsOfTheYear)vals.GetValue(i)) > 0)
					s.Add(DateTimeFormatInfo.CurrentInfo.GetMonthName(i+1));
			}

			return string.Join(Resources.ListSeparator, s.ToArray());
		}

		private static string BuildEnumString(string preface, object enumValue)
		{
			string[] vals = enumValue.ToString().Split(new string[] { ", " }, StringSplitOptions.None);
			if (vals.Length == 0)
				return string.Empty;
			for (int i = 0; i < vals.Length; i++)
				vals[i] = Resources.ResourceManager.GetString(preface + vals[i]);
			return string.Join(Resources.ListSeparator, vals);
		}
	}
}
