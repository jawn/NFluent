﻿ // --------------------------------------------------------------------------------------------------------------------
 // <copyright file="AsOperatorTests.cs" company="">
 //   Copyright 2014 Thomas PIERRAIN, Cyrille DUPUYDAUBY
 //   Licensed under the Apache License, Version 2.0 (the "License");
 //   you may not use this file except in compliance with the License.
 //   You may obtain a copy of the License at
 //       http://www.apache.org/licenses/LICENSE-2.0
 //   Unless required by applicable law or agreed to in writing, software
 //   distributed under the License is distributed on an "AS IS" BASIS,
 //   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 //   See the License for the specific language governing permissions and
 //   limitations under the License.
 // </copyright>
 // --------------------------------------------------------------------------------------------------------------------

namespace NFluent.Tests
{
    using System;
    using NFluent.Helpers;
    using NUnit.Framework;

    [TestFixture]
    public class AsOperatorTests
    {
        [Test]
        public void ShouldProvideANameForTheSut()
        {
            Check.ThatCode( () => Check.That(42).As("answer").IsAfter(100))
                .IsAFaillingCheckWithMessage("",
                    "The checked [answer] is not after the reference value.",
                    "The checked [answer]:",
                    "\t[42]",
                    "The expected [answer]: after",
                    "\t[100]");
        }

        [Test]
        // GH #259 As must support not
        public void ShouldProvideANameForTheSutAndWorkWithNot()
        {
            Check.ThatCode( () => Check.That(42).As("answer").Not.IsBefore(100))
                .IsAFaillingCheckWithMessage("",
                    "The checked [answer] is before the reference value whereas it must not.",
                    "The checked [answer]:",
                    "\t[42]",
                    "The expected [answer]: after",
                    "\t[100]");
        }

        [Test]
        public void
            ShouldOfferCustomMessage()
        {
            Check.ThatCode(() =>
            {
                Check.WithCustomMessage("We should get 2.").That(1).IsEqualTo(2); 
            }).IsAFaillingCheckWithMessage("We should get 2.",
                "The checked value is different from the expected one.", 
                "The checked value:", 
                "\t[1]", 
                "The expected value:", 
                "\t[2]");
        }
        [Test]
        public void
            ShouldOfferCustomMessageForEnum()
        {
            Check.ThatCode(() =>
            {
                Check.WithCustomMessage("We should get 2.").ThatEnum(1).IsEqualTo(2); 
            }).IsAFaillingCheckWithMessage("We should get 2.",
                "The checked value is different from the expected one.", 
                "The checked value:", 
                "\t[1]", 
                "The expected value:", 
                "\t[2]");
        }

        [Test]
        public void
            ShouldOfferCustomMessageForActions()
        {
            Check.ThatCode(() =>
            {
                Check.WithCustomMessage("This should work.").ThatCode(() => throw new Exception()).DoesNotThrow(); 
            }).IsAFaillingCheckWithMessage("This should work.",
                "The checked code raised an exception, whereas it must not.",
                "The checked code's raised exception:", 
                "*");
        }

        [Test]
        public void
            ShouldOfferCustomMessageForFunctions()
        {
            Check.ThatCode(() =>
                {
                    Check.WithCustomMessage("We should get 2.").ThatCode(() => 1).WhichResult().IsEqualTo(2);
                }).IsAFaillingCheckWithMessage("We should get 2.",
                "The checked value is different from the expected one.", 
                "The checked value:", 
                "\t[1]", 
                "The expected value:", 
                "\t[2]");
        }
    }
}
