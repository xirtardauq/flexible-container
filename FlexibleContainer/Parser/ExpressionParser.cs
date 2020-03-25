﻿using FlexibleContainer.Parser.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FlexibleContainer.Parser
{
    public class ExpressionParser
    {
        private static readonly Regex TagRegex = new Regex(
            @"^" +
            @"(?<tag>\S+?)" + 
            @"(?<id>#\S+?)?" +
            @"(?<class>\.\S+?){0,}" +
            @"(\[((?<attr>[^=\s]+(=""[^""]*"")?)\s?){0,}\])?" +
            @"({(?<content>.+)})?" +
            @"$",
            RegexOptions.Compiled | RegexOptions.Singleline);

        public static Node Parse(string expression)
        {
            var root = CreateNode("root");
            var expressions = SplitExpressionAt(expression, '>');
            root.Children = ParseInner(expressions);
            return root;
        }

        private static List<Node> ParseInner(List<string> expressions)
        {
            if (expressions.Count < 1)
            {
                return new List<Node>();
            }

            var firstExpression = expressions[0];
            var firstSiblings = SplitExpressionAt(firstExpression, '+');
            if (expressions.Count == 1 && firstSiblings.Count == 1)
            {
                return new List<Node>()
                {
                    CreateNode(firstSiblings[0])
                };
            }

            var result = new List<Node>();
            foreach (var sibling in firstSiblings)
            {
                var siblingExpressions = SplitExpressionAt(TrimParenthesis(sibling), '>');
                var nodes = ParseInner(siblingExpressions);
                result.AddRange(nodes);
            }

            var restExpressions = expressions.GetRange(1, expressions.Count - 1);
            if (result.Count > 0 && restExpressions.Count > 0)
            {
                var nodes = ParseInner(restExpressions);
                var lastNode = result[result.Count - 1];
                lastNode.Children = nodes;
            }

            return result;

            string TrimParenthesis(string value)
            {
                return value.Length > 1 && value[0] == '(' && value[value.Length - 1] == ')'
                    ? value.Substring(1, value.Length - 2)
                    : value;
            }
        }

        private static Node CreateNode(string node)
        {
            var tagMatch = TagRegex.Match(node);
            if (!tagMatch.Success)
            {
                throw new FormatException($"Invalid format of the node expression (Expression: {node})");
            }

            return new Node
            {
                Tag = tagMatch.Groups["tag"].Value,
                Id = tagMatch.Groups["id"].Value,
                ClassList = GetCaptureValues(tagMatch, "class"),
                Attributes = GetCaptureValues(tagMatch, "attr").Select(ParseAttribute).ToDictionary(attr => attr.name, attr => attr.value),
                Content = tagMatch.Groups["content"].Value,
            };

            ICollection<string> GetCaptureValues(Match m, string groupName)
            {
                return m.Groups[groupName].Captures
                    .OfType<Capture>()
                    .Select(capture => capture.Value)
                    .ToList();
            }

            (string name, string value) ParseAttribute(string raw)
            {
                var splitted = raw.Split('=');
                return splitted.Length <= 1
                    ? (splitted[0], null)
                    : (splitted[0], splitted[1].Trim('"'));
            }
        }

        private static List<string> SplitExpressionAt(string expression, char delimiter)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            var nest = 0;
            var inContent = false;
            var inAttr = false;
            foreach (var character in expression)
            {
                // Update status
                switch (character)
                {
                    case '{':
                        if (!inContent && !inAttr)
                        {
                            inContent = true;
                        }
                        break;
                    case '}':
                        if (inContent && !inAttr)
                        {
                            inContent = false;
                        }
                        break;
                    case '[':
                        if (!inAttr && !inContent)
                        {
                            inAttr = true;
                        }
                        break;
                    case ']':
                        if (inAttr && !inContent)
                        {
                            inAttr = false;
                        }
                        break;
                    case '(':
                        if (!inContent && !inAttr)
                        {
                            nest++;
                        }
                        break;
                    case ')':
                        if (!inContent && !inAttr)
                        {
                            nest--;
                        }
                        break;
                }

                if (character != delimiter || inContent || inAttr || nest > 0)
                {
                    sb.Append(character);
                    continue;
                }

                result.Add(sb.ToString());
                sb.Clear();
            }

            if (sb.Length > 0)
            {
                result.Add(sb.ToString());
            }

            if (nest < 0)
            {
                throw new FormatException($"Too much open parenthesis (Expression: {expression})");
            }

            if (nest > 0)
            {
                throw new FormatException($"Too much close parenthesis (Expression: {expression})");
            }

            return result;
        }
    }
}
