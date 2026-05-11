// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#include <codecvt>
#include <fstream>
#include <locale>
#include <memory>
#include <regex>
#include <string>
#include <string_view>
#include <unordered_map>
#include <vector>

#include "CalcManager/CalculatorManager.h"
#include "CalcManager/CalculatorResource.h"

using CalculationManager::CalculatorManager;
using CalculationManager::Command;
using CalculationManager::IResourceProvider;

namespace
{
    std::string ToUtf8(std::wstring_view value)
    {
        std::wstring_convert<std::codecvt_utf8_utf16<wchar_t>> converter;
        return converter.to_bytes(value.data(), value.data() + value.size());
    }

    std::wstring FromUtf8(std::string_view value)
    {
        std::wstring_convert<std::codecvt_utf8_utf16<wchar_t>> converter;
        return converter.from_bytes(value.data(), value.data() + value.size());
    }

    std::string XmlDecode(std::string value)
    {
        const std::pair<std::string, std::string> replacements[] = {
            { "&lt;", "<" },
            { "&gt;", ">" },
            { "&amp;", "&" },
            { "&quot;", "\"" },
            { "&apos;", "'" },
        };

        for (auto const& [from, to] : replacements)
        {
            size_t pos = 0;
            while ((pos = value.find(from, pos)) != std::string::npos)
            {
                value.replace(pos, from.size(), to);
                pos += to.size();
            }
        }

        return value;
    }

    class ResourceProvider final : public IResourceProvider
    {
    public:
        ResourceProvider()
        {
            LoadResw("src/Calculator/Resources/en-US/CEngineStrings.resw");
            AddFallbacks();
        }

        std::wstring GetCEngineString(std::wstring_view id) override
        {
            if (id == L"sDecimal")
            {
                return L".";
            }
            if (id == L"sThousand")
            {
                return L",";
            }
            if (id == L"sGrouping")
            {
                return L"3;0";
            }

            auto iter = m_strings.find(std::wstring(id));
            return iter == m_strings.end() ? std::wstring{} : iter->second;
        }

    private:
        std::unordered_map<std::wstring, std::wstring> m_strings;

        void LoadResw(const char* path)
        {
            std::ifstream file(path);
            if (!file)
            {
                return;
            }

            std::string xml((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
            std::regex dataRegex("<data name=\"([^\"]+)\"[^>]*>\\s*<value>(.*?)</value>", std::regex::icase);
            for (auto iter = std::sregex_iterator(xml.begin(), xml.end(), dataRegex); iter != std::sregex_iterator(); ++iter)
            {
                m_strings[FromUtf8((*iter)[1].str())] = FromUtf8(XmlDecode((*iter)[2].str()));
            }
        }

        void AddFallbacks()
        {
            const std::pair<const wchar_t*, const wchar_t*> values[] = {
                { L"0", L"+/-" }, { L"1", L"C" }, { L"2", L"CE" }, { L"3", L"Backspace" }, { L"4", L"." },
                { L"5", L"" }, { L"11", L"/" }, { L"12", L"*" }, { L"13", L"+" }, { L"14", L"-" },
                { L"17", L"^" }, { L"22", L"sin" }, { L"23", L"cos" }, { L"24", L"tan" },
                { L"28", L"ln" }, { L"29", L"log" }, { L"30", L"sqrt" }, { L"31", L"x^2" },
                { L"33", L"n!" }, { L"34", L"1/" }, { L"37", L"10^" }, { L"38", L"%" },
                { L"40", L"pi" }, { L"41", L"=" }, { L"47", L"Exp" }, { L"48", L"(" }, { L"49", L")" },
                { L"50", L"0" }, { L"51", L"1" }, { L"52", L"2" }, { L"53", L"3" }, { L"54", L"4" },
                { L"55", L"5" }, { L"56", L"6" }, { L"57", L"7" }, { L"58", L"8" }, { L"59", L"9" },
                { L"60", L"A" }, { L"61", L"B" }, { L"62", L"C" }, { L"63", L"D" }, { L"64", L"E" },
                { L"65", L"F" }, { L"97", L"negate" }, { L"99", L"Cannot divide by zero" },
                { L"100", L"Invalid input" }, { L"101", L"Result is undefined" }, { L"105", L"Not enough memory" },
                { L"107", L"Overflow" }, { L"108", L"Result not defined" }, { L"SecDeg", L"sec" },
                { L"Abs", L"abs" }, { L"TwoPowX", L"2^" }, { L"CubeRoot", L"cuberoot" },
            };

            for (auto const& [key, value] : values)
            {
                m_strings.try_emplace(key, value);
            }
        }
    };

    class Display final : public ICalcDisplay
    {
    public:
        void SetPrimaryDisplay(const std::wstring& displayString, bool isError) override
        {
            primary = ToUtf8(displayString);
            inError = isError;
        }

        void SetIsInError(bool isInError) override
        {
            inError = isInError;
        }

        void SetExpressionDisplay(
            std::shared_ptr<std::vector<std::pair<std::wstring, int>>> const& tokens,
            std::shared_ptr<std::vector<std::shared_ptr<IExpressionCommand>>> const&) override
        {
            std::wstring text;
            if (tokens)
            {
                for (auto const& token : *tokens)
                {
                    text += token.first;
                }
            }
            expression = ToUtf8(text);
        }

        void SetParenthesisNumber(unsigned int) override {}
        void OnNoRightParenAdded() override {}
        void MaxDigitsReached() override {}
        void BinaryOperatorReceived() override {}
        void OnHistoryItemAdded(unsigned int) override {}
        void SetMemorizedNumbers(const std::vector<std::wstring>&) override {}
        void MemoryItemChanged(unsigned int) override {}
        void InputChanged() override {}

        std::string primary = "0";
        std::string expression;
        bool inError = false;
    };

    struct NativeCalculator
    {
        ResourceProvider resources;
        Display display;
        std::unique_ptr<CalculatorManager> manager;
        bool scientific = false;

        NativeCalculator()
            : manager(std::make_unique<CalculatorManager>(&display, &resources))
        {
            manager->Reset();
        }
    };

    bool TryMapCommand(std::string_view key, Command& command)
    {
        static const std::unordered_map<std::string_view, Command> commands = {
            { "0", Command::Command0 }, { "1", Command::Command1 }, { "2", Command::Command2 }, { "3", Command::Command3 },
            { "4", Command::Command4 }, { "5", Command::Command5 }, { "6", Command::Command6 }, { "7", Command::Command7 },
            { "8", Command::Command8 }, { "9", Command::Command9 }, { ".", Command::CommandPNT }, { "Sign", Command::CommandSIGN },
            { "Back", Command::CommandBACK }, { "C", Command::CommandCLEAR }, { "CE", Command::CommandCENTR },
            { "+", Command::CommandADD }, { "-", Command::CommandSUB }, { "*", Command::CommandMUL }, { "/", Command::CommandDIV },
            { "=", Command::CommandEQU }, { "%", Command::CommandPERCENT }, { "Square", Command::CommandSQR },
            { "Sqrt", Command::CommandSQRT }, { "Reciprocal", Command::CommandREC }, { "Pi", Command::CommandPI },
            { "Sin", Command::CommandSIN }, { "Cos", Command::CommandCOS }, { "Tan", Command::CommandTAN },
            { "Log", Command::CommandLOG }, { "Ln", Command::CommandLN }, { "Exp", Command::CommandEXP },
            { "TenPow", Command::CommandPOW10 }, { "Factorial", Command::CommandFAC }, { "Mod", Command::CommandMOD },
            { "Pow", Command::CommandPWR }, { "Abs", Command::CommandAbs }, { "E", Command::CommandEuler },
        };

        auto iter = commands.find(key);
        if (iter == commands.end())
        {
            return false;
        }

        command = iter->second;
        return true;
    }
}

extern "C"
{
    NativeCalculator* calc_create()
    {
        try
        {
            return new NativeCalculator();
        }
        catch (...)
        {
            return nullptr;
        }
    }

    void calc_destroy(NativeCalculator* calculator)
    {
        delete calculator;
    }

    void calc_reset(NativeCalculator* calculator)
    {
        if (!calculator)
        {
            return;
        }
        calculator->manager->Reset();
        if (calculator->scientific)
        {
            calculator->manager->SetScientificMode();
        }
    }

    void calc_set_mode(NativeCalculator* calculator, int scientific)
    {
        if (!calculator)
        {
            return;
        }
        calculator->scientific = scientific != 0;
        if (calculator->scientific)
        {
            calculator->manager->SetScientificMode();
        }
        else
        {
            calculator->manager->SetStandardMode();
        }
    }

    int calc_send_key(NativeCalculator* calculator, const char* key)
    {
        if (!calculator || !key)
        {
            return 0;
        }

        try
        {
            Command command{};
            if (!TryMapCommand(key, command))
            {
                return 0;
            }

            calculator->manager->SendCommand(command);
            return 1;
        }
        catch (...)
        {
            return 0;
        }
    }

    const char* calc_get_display(NativeCalculator* calculator)
    {
        return calculator ? calculator->display.primary.c_str() : "0";
    }

    const char* calc_get_expression(NativeCalculator* calculator)
    {
        return calculator ? calculator->display.expression.c_str() : "";
    }

    int calc_is_error(NativeCalculator* calculator)
    {
        return calculator && calculator->display.inError ? 1 : 0;
    }
}
