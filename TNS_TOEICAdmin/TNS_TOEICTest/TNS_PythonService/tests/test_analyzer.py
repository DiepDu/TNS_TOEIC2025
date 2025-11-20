import pytest
from app.models import ResponseData, AnalysisRequest
from app.irt_analyzer import calculate_theta, interpret_theta
from app.behavior_analyzer import analyze_behavior
from app.weakness_analyzer import analyze_weaknesses

def test_irt_calculation():
    """Test IRT theta calculation"""
    responses = [
        ResponseData(
            isCorrect=1,
            timeSpent=30,
            numberOfAnswerChanges=0,
            difficulty=0.5,
            part=5
        )
        for _ in range(60)  # 60 correct answers
    ]
    
    theta = calculate_theta(responses, initial_theta=0.0)
    assert theta > 0.0  # Should be positive for all correct
    assert -3.0 <= theta <= 3.0

def test_theta_interpretation():
    """Test theta to level conversion"""
    assert "Beginner" in interpret_theta(-2.0)
    assert "Intermediate" in interpret_theta(0.0)
    assert "Advanced" in interpret_theta(2.0)

def test_behavior_analysis():
    """Test behavioral analysis"""
    responses = [
        ResponseData(
            isCorrect=1,
            timeSpent=25,
            numberOfAnswerChanges=0,
            difficulty=0.0,
            part=5
        )
        for _ in range(30)
    ]
    
    behavior_scores, patterns = analyze_behavior(responses)
    
    assert behavior_scores.accuracy == 100.0
    assert behavior_scores.speed > 0
    assert behavior_scores.decisiveness > 0

def test_weakness_detection():
    """Test weakness analysis"""
    responses = [
        ResponseData(
            isCorrect=0,
            timeSpent=30,
            numberOfAnswerChanges=1,
            difficulty=0.0,
            grammarName="Tenses",
            vocabName="Business",
            part=5
        )
        for _ in range(10)
    ]
    
    weaknesses = analyze_weaknesses(responses)
    
    assert len(weaknesses.top_grammar) > 0
    assert "Tenses" in weaknesses.top_grammar[0]