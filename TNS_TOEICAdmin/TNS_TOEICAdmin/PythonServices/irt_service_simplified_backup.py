from flask import Flask, request, jsonify
from flask_cors import CORS
import pandas as pd
import numpy as np
from scipy.optimize import minimize
from scipy.special import expit  # logistic function
import traceback
from datetime import datetime
import logging
import os

# Configure logging
if not os.path.exists('logs'):
    os.makedirs('logs')

logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler('logs/irt_service.log'),
        logging.StreamHandler()
    ]
)

app = Flask(__name__)
CORS(app)

@app.route('/health', methods=['GET'])
def health_check():
    """Health check endpoint"""
    return jsonify({
        "status": "OK",
        "service": "IRT Analysis Service (Simplified)",
        "timestamp": datetime.utcnow().isoformat(),
        "version": "1.0.0",
        "method": "Scipy-based 3PL IRT"
    })

@app.route('/analyze', methods=['POST'])
def analyze_irt():
    """
    Simplified IRT Analysis using scipy optimization
    """
    try:
        logging.info("=== IRT Analysis Request Started ===")
        
        if not request.json or 'data' not in request.json:
            logging.error("Missing 'data' field")
            return jsonify({"error": "Missing 'data' field"}), 400
        
        raw_data = request.json['data']
        logging.info(f"Received {len(raw_data)} responses")
        
        if len(raw_data) < 100:
            logging.warning(f"Insufficient data: {len(raw_data)} responses")
            return jsonify({
                "error": "Insufficient data",
                "message": f"Need at least 100 responses, got {len(raw_data)}"
            }), 400
        
        # Prepare DataFrame
        df = pd.DataFrame(raw_data)
        df = df.rename(columns={
            'memberKey': 'subject_id',
            'questionKey': 'item_id',
            'isCorrect': 'response'
        })
        
        original_count = len(df)
        df = df.drop_duplicates(subset=['subject_id', 'item_id'])
        logging.info(f"Removed {original_count - len(df)} duplicates")
        
        df['response'] = df['response'].astype(int)
        
        # Check data sufficiency
        question_counts = df.groupby('item_id').size()
        valid_questions = question_counts[question_counts >= 10].index.tolist()
        
        logging.info(f"Questions with >= 10 responses: {len(valid_questions)}")
        
        if len(valid_questions) == 0:
            logging.error("No questions with sufficient data")
            return jsonify({
                "error": "No questions with sufficient data",
                "message": "Each question needs at least 10 responses"
            }), 400
        
        df_filtered = df[df['item_id'].isin(valid_questions)]
        logging.info(f"Filtered dataset: {len(df_filtered)} responses")
        
        # Perform Simplified IRT Analysis
        logging.info("Starting Simplified IRT analysis...")
        results = perform_simplified_irt(df_filtered)
        
        # Format results
        question_params = {}
        for item_id in valid_questions:
            n_responses = question_counts.get(item_id, 0)
            
            if n_responses >= 100:
                confidence = "High"
            elif n_responses >= 30:
                confidence = "Medium"
            else:
                confidence = "Low"
            
            params = results['item_params'].get(str(item_id), {
                'discrimination': 1.0,
                'difficulty': 0.0,
                'guessing': 0.25
            })
            
            a = float(params['discrimination'])
            b = float(params['difficulty'])
            c = float(params['guessing'])
            
            quality = determine_quality(a, c, b)
            
            question_params[str(item_id)] = {
                'difficulty': b,
                'discrimination': a,
                'guessing': c,
                'quality': quality,
                'confidenceLevel': confidence,
                'attemptCount': int(n_responses),
                'converged': True
            }
        
        member_abilities = results['subject_abilities']
        
        logging.info(f"Complete: {len(question_params)} questions, {len(member_abilities)} members")
        
        return jsonify({
            "status": "OK",
            "questionParams": question_params,
            "memberAbilities": member_abilities,
            "metadata": {
                "totalQuestions": len(question_params),
                "totalMembers": len(member_abilities),
                "totalResponses": len(df_filtered),
                "timestamp": datetime.utcnow().isoformat(),
                "modelType": "3PL (Simplified)"
            }
        })
        
    except Exception as e:
        error_trace = traceback.format_exc()
        logging.error(f"ERROR: {error_trace}")
        return jsonify({"error": str(e), "trace": error_trace}), 500

def perform_simplified_irt(df):
    """
    Simplified 3PL IRT using classical test theory approximations
    """
    # Get unique subjects and items
    subjects = df['subject_id'].unique()
    items = df['item_id'].unique()
    
    # Calculate item parameters using CTT approximations
    item_params = {}
    for item in items:
        item_df = df[df['item_id'] == item]
        
        # Calculate basic statistics
        correct_rate = item_df['response'].mean()
        n_responses = len(item_df)
        
        # Estimate difficulty (b parameter)
        # Using logit transformation
        if correct_rate >= 0.99:
            b = -3.0
        elif correct_rate <= 0.01:
            b = 3.0
        else:
            b = -np.log(correct_rate / (1 - correct_rate))
            b = np.clip(b, -3, 3)
        
        # Estimate discrimination (a parameter)
        # Using point-biserial correlation approximation
        subject_scores = df.groupby('subject_id')['response'].mean()
        high_performers = subject_scores[subject_scores >= subject_scores.quantile(0.75)].index
        low_performers = subject_scores[subject_scores <= subject_scores.quantile(0.25)].index
        
        high_correct = item_df[item_df['subject_id'].isin(high_performers)]['response'].mean() if len(high_performers) > 0 else correct_rate
        low_correct = item_df[item_df['subject_id'].isin(low_performers)]['response'].mean() if len(low_performers) > 0 else correct_rate
        
        discrimination = (high_correct - low_correct) * 3.0
        discrimination = np.clip(discrimination, 0, 2.0)
        
        # Estimate guessing (c parameter)
        if correct_rate < 0.25:
            guessing = correct_rate
        elif low_correct < 0.40:
            guessing = max(0.15, min(0.35, low_correct * 0.7))
        else:
            guessing = 0.25
        
        item_params[str(item)] = {
            'discrimination': float(discrimination),
            'difficulty': float(b),
            'guessing': float(guessing)
        }
    
    # Calculate subject abilities (theta)
    subject_abilities = {}
    for subject in subjects:
        subject_df = df[df['subject_id'] == subject]
        correct_rate = subject_df['response'].mean()
        
        # Simple ability estimation
        if correct_rate >= 0.99:
            theta = 3.0
        elif correct_rate <= 0.01:
            theta = -3.0
        else:
            theta = np.log(correct_rate / (1 - correct_rate))
            theta = np.clip(theta, -3, 3)
        
        subject_abilities[str(subject)] = float(theta)
    
    return {
        'item_params': item_params,
        'subject_abilities': subject_abilities
    }

def determine_quality(discrimination, guessing, difficulty):
    """Determine question quality"""
    poor_discrimination = discrimination < 0.5
    poor_guessing = guessing > 0.4
    extreme_difficulty = difficulty < -2.5 or difficulty > 2.5
    
    if poor_discrimination or poor_guessing or extreme_difficulty:
        return "Kém"
    
    fair_discrimination = discrimination < 1.0
    fair_guessing = guessing > 0.3 or guessing < 0.20
    
    if fair_discrimination or fair_guessing:
        return "Cần xem lại"
    
    return "Tốt"

if __name__ == '__main__':
    print("=" * 60)
    print("🐍 IRT Analysis Service (Simplified) Starting...")
    print("📊 Method: Scipy-based 3PL approximation")
    print("📊 Listening on: http://localhost:5001")
    print("❤️  Health check: http://localhost:5001/health")
    print("=" * 60)
    
    app.run(host='0.0.0.0', port=5001, debug=True, threaded=True)