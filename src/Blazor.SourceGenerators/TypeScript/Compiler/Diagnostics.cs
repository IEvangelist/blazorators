// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.TypeScript.Types;

namespace Blazor.SourceGenerators.TypeScript.Compiler;

internal class Diagnostics
{
    public static DiagnosticMessage Merge_conflict_marker_encountered =
        DiagnosticMessage.Error("Merge conflict marker encountered.", 1185);

    public static DiagnosticMessage Digit_expected =
        DiagnosticMessage.Error("Digit expected.", 1124);

    public static DiagnosticMessage Unterminated_string_literal =
        DiagnosticMessage.Error("Unterminated string literal.", 1002);

    public static DiagnosticMessage Unterminated_template_literal =
        DiagnosticMessage.Error("Unterminated template literal.", 1160);
    
    public static DiagnosticMessage Unexpected_end_of_text =
        DiagnosticMessage.Error("Unexpected end of text.", 1126);

    public static DiagnosticMessage Hexadecimal_digit_expected =
        DiagnosticMessage.Error("Hexadecimal digit expected.", 1125);

    public static DiagnosticMessage An_extended_Unicode_escape_value_must_be_between_0x0_and_0x10FFFF_inclusive =
        DiagnosticMessage.Error("An extended Unicode escape value must be between 0x0 and 0x10FFFF inclusive.", 1198);

    public static DiagnosticMessage Unterminated_Unicode_escape_sequence =
        DiagnosticMessage.Error("Unterminated Unicode escape sequence.", 1199);

    public static DiagnosticMessage Asterisk_Slash_expected;

    public static DiagnosticMessage Binary_digit_expected;

    public static DiagnosticMessage Octal_digit_expected;

    public static DiagnosticMessage Invalid_character;
    
    public static DiagnosticMessage Unterminated_regular_expression_literal;
    
    public static DiagnosticMessage _0_expected;

    public static DiagnosticMessage Identifier_expected;

    public static DiagnosticMessage Declaration_or_statement_expected;

    public static DiagnosticMessage case_or_default_expected;

    public static DiagnosticMessage Statement_expected;

    public static DiagnosticMessage Property_or_signature_expected;

    public static DiagnosticMessage Unexpected_token_A_constructor_method_accessor_or_property_was_expected;

    public static DiagnosticMessage Enum_member_expected;

    public static DiagnosticMessage Expression_expected;

    public static DiagnosticMessage Variable_declaration_expected;
    
    public static DiagnosticMessage Property_destructuring_pattern_expected;

    public static DiagnosticMessage Array_element_destructuring_pattern_expected;

    public static DiagnosticMessage Argument_expression_expected;

    public static DiagnosticMessage Property_assignment_expected;

    public static DiagnosticMessage Expression_or_comma_expected;

    public static DiagnosticMessage Parameter_declaration_expected;

    public static DiagnosticMessage Type_parameter_declaration_expected;

    public static DiagnosticMessage Type_argument_expected;

    public static DiagnosticMessage Type_expected;

    public static DiagnosticMessage Unexpected_token_expected;

    public static DiagnosticMessage A_type_assertion_expression_is_not_allowed_in_the_left_hand_side_of_an_exponentiation_expression_Consider_enclosing_the_expression_in_parentheses;

    public static DiagnosticMessage An_unary_expression_with_the_0_operator_is_not_allowed_in_the_left_hand_side_of_an_exponentiation_expression_Consider_enclosing_the_expression_in_parentheses;

    public static DiagnosticMessage super_must_be_followed_by_an_argument_list_or_member_access;
    
    public static DiagnosticMessage Expected_corresponding_JSX_closing_tag_for_0;
    
    public static DiagnosticMessage JSX_expressions_must_have_one_parent_element;

    public static DiagnosticMessage JSX_element_0_has_no_corresponding_closing_tag;

    public static DiagnosticMessage Declaration_expected;

    public static DiagnosticMessage or_expected;

    public static DiagnosticMessage An_AMD_module_cannot_have_multiple_name_assignments;

    public static DiagnosticMessage Type_argument_list_cannot_be_empty;

    public static DiagnosticMessage Trailing_comma_not_allowed;

    public static DiagnosticMessage _0_tag_already_specified;
}