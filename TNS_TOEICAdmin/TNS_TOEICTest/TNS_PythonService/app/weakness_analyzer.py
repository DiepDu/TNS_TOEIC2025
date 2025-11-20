from typing import List
from collections import Counter
from app.models import ResponseData, WeaknessAnalysis

def analyze_weaknesses(responses: List[ResponseData]) -> WeaknessAnalysis:
    """
    Detect grammar, vocabulary, and error patterns
    """
    if not responses:
        return WeaknessAnalysis()
    
    # Only analyze incorrect answers
    wrong_answers = [r for r in responses if r.isCorrect == 0]
    
    if not wrong_answers:
        return WeaknessAnalysis(summary="Excellent! No errors detected.")
    
    # Count occurrences
    grammar_counts = Counter(r.grammarName for r in wrong_answers if r.grammarName)
    vocab_counts = Counter(r.vocabName for r in wrong_answers if r.vocabName)
    error_counts = Counter(r.errorType for r in wrong_answers if r.errorType)
    category_counts = Counter(r.categoryName for r in wrong_answers if r.categoryName)
    
    # Top 5
    top_grammar = [f"{name} ({count} errors)" for name, count in grammar_counts.most_common(5)]
    top_vocab = [f"{name} ({count} errors)" for name, count in vocab_counts.most_common(5)]
    top_errors = [f"{name} ({count} times)" for name, count in error_counts.most_common(5)]
    top_categories = [f"{name} ({count} errors)" for name, count in category_counts.most_common(3)]
    
    # Summary
    total_errors = len(wrong_answers)
    accuracy = ((len(responses) - total_errors) / len(responses)) * 100
    
    summary = f"Accuracy: {accuracy:.1f}% ({total_errors}/{len(responses)} errors). "
    
    if top_grammar:
        summary += f"Main grammar issue: {grammar_counts.most_common(1)[0][0]}. "
    if top_vocab:
        summary += f"Vocabulary gap: {vocab_counts.most_common(1)[0][0]}."
    
    return WeaknessAnalysis(
        top_grammar=top_grammar,
        top_vocab=top_vocab,
        top_error_types=top_errors,
        top_categories=top_categories,
        summary=summary.strip()
    )