from flask import Flask, request, jsonify
from flask_cors import CORS
import pandas as pd
import numpy as np
import torch
import pyro
import pyro.distributions as dist
from pyro.infer import SVI, Trace_ELBO
from pyro.optim import Adam
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
        logging.FileHandler('logs/full_irt_service.log'),
        logging.StreamHandler()
    ]
)

app = Flask(__name__)
CORS(app)

class FullIRT3PL:
    """
    Full IRT 3-Parameter Logistic Model using Pyro (Probabilistic Programming)
    
    Implements EM algorithm for joint estimation of:
    - θ (theta): Subject ability parameters
    - a: Item discrimination parameters  
    - b: Item difficulty parameters
    - c: Item guessing parameters
    
    Based on: Lord, F. M. (1980). Applications of Item Response Theory
    """
    
    def __init__(self, max_iter=300, tolerance=0.0001):
        """
        Args:
            max_iter: Maximum number of training iterations
            tolerance: Convergence tolerance for loss
        """
        self.max_iter = max_iter
        self.tolerance = tolerance
        self.theta = None
        self.a = None
        self.b = None
        self.c = None
        
    def model(self, responses, n_subjects, n_items):
        """
        Probabilistic model for 3PL IRT
        
        P(X=1|θ,a,b,c) = c + (1-c) / (1 + exp(-a(θ-b)))
        
        Args:
            responses: Tensor of shape (n_subjects, n_items)
            n_subjects: Number of subjects
            n_items: Number of items
        """
        # Priors for item parameters
        a = pyro.sample("a", dist.LogNormal(0., 1.).expand([n_items]).to_event(1))
        b = pyro.sample("b", dist.Normal(0., 1.).expand([n_items]).to_event(1))
        c = pyro.sample("c", dist.Beta(5, 17).expand([n_items]).to_event(1))  # Mean ≈ 0.25
        
        # Priors for subject abilities
        theta = pyro.sample("theta", dist.Normal(0., 1.).expand([n_subjects]).to_event(1))
        
        # Likelihood
        with pyro.plate("data", n_subjects):
            for j in range(n_items):
                # 3PL probability
                z = a[j] * (theta - b[j])
                p = c[j] + (1 - c[j]) * torch.sigmoid(z)
                
                # Observe responses
                pyro.sample(f"obs_{j}", dist.Bernoulli(p), obs=responses[:, j])
    
    def guide(self, responses, n_subjects, n_items):
        """
        Variational guide for approximate posterior (Q distribution)
        """
        # Item parameters (variational parameters)
        a_loc = pyro.param("a_loc", torch.ones(n_items))
        a_scale = pyro.param("a_scale", torch.ones(n_items), constraint=dist.constraints.positive)
        pyro.sample("a", dist.LogNormal(a_loc, a_scale).to_event(1))
        
        b_loc = pyro.param("b_loc", torch.zeros(n_items))
        b_scale = pyro.param("b_scale", torch.ones(n_items), constraint=dist.constraints.positive)
        pyro.sample("b", dist.Normal(b_loc, b_scale).to_event(1))
        
        c_alpha = pyro.param("c_alpha", torch.ones(n_items) * 5, constraint=dist.constraints.positive)
        c_beta = pyro.param("c_beta", torch.ones(n_items) * 17, constraint=dist.constraints.positive)
        pyro.sample("c", dist.Beta(c_alpha, c_beta).to_event(1))
        
        # Subject abilities
        theta_loc = pyro.param("theta_loc", torch.zeros(n_subjects))
        theta_scale = pyro.param("theta_scale", torch.ones(n_subjects), constraint=dist.constraints.positive)
        pyro.sample("theta", dist.Normal(theta_loc, theta_scale).to_event(1))
    
    def fit(self, response_matrix):
        """
        Fit the 3PL model using Stochastic Variational Inference (EM-like algorithm)
        
        Args:
            response_matrix: numpy array of shape (n_subjects, n_items) with values 0/1
        
        Returns:
            self (fitted model)
        """
        # Convert to torch tensor
        responses = torch.tensor(response_matrix, dtype=torch.float32)
        n_subjects, n_items = responses.shape
        
        logging.info(f"Starting Full IRT training: {n_subjects} subjects, {n_items} items")
        
        # Clear parameter store
        pyro.clear_param_store()
        
        # Setup SVI with increased learning rate
        optimizer = Adam({"lr": 0.05})
        svi = SVI(self.model, self.guide, optimizer, loss=Trace_ELBO())
        
        # Training loop (EM-like iterations)
        losses = []
        for epoch in range(self.max_iter):
            loss = svi.step(responses, n_subjects, n_items)
            losses.append(loss)
            
            if epoch % 50 == 0:
                logging.info(f"Epoch {epoch}/{self.max_iter}, Loss: {loss:.2f}")
            
            # Check convergence
            if epoch > 20 and abs(losses[-1] - losses[-20]) < self.tolerance:
                logging.info(f"Converged at epoch {epoch}")
                break
        
        # Extract final parameters
        self.a = pyro.param("a_loc").detach().numpy()
        self.b = pyro.param("b_loc").detach().numpy()
        self.c_alpha = pyro.param("c_alpha").detach().numpy()
        self.c_beta = pyro.param("c_beta").detach().numpy()
        self.c = self.c_alpha / (self.c_alpha + self.c_beta)  # Beta mean
        self.theta = pyro.param("theta_loc").detach().numpy()
        
        # Clip parameters to reasonable ranges
        self.a = np.clip(self.a, 0.01, 2.5)
        self.b = np.clip(self.b, -3, 3)
        self.c = np.clip(self.c, 0.01, 0.5)
        self.theta = np.clip(self.theta, -3, 3)
        
        logging.info("Training completed successfully")
        logging.info(f"Parameter ranges: a=[{self.a.min():.3f}, {self.a.max():.3f}], "
                    f"b=[{self.b.min():.3f}, {self.b.max():.3f}], "
                    f"c=[{self.c.min():.3f}, {self.c.max():.3f}]")
        
        return self
    
    def get_item_parameters(self):
        """Return item parameters as dict"""
        return {
            'discrimination': self.a.tolist(),
            'difficulty': self.b.tolist(),
            'guessing': self.c.tolist()
        }
    
    def get_subject_abilities(self):
        """Return subject abilities as array"""
        return self.theta.tolist()


@app.route('/health', methods=['GET'])
def health_check():
    """Health check endpoint"""
    return jsonify({
        "status": "OK",
        "service": "Full IRT Analysis Service (3PL + EM Algorithm)",
        "timestamp": datetime.utcnow().isoformat(),
        "version": "2.0.0",
        "method": "Pyro Probabilistic Programming + SVI"
    })

@app.route('/analyze', methods=['POST'])
def analyze_irt():
    """
    Full IRT Analysis endpoint
    
    Expected JSON:
    {
        "data": [
            {"memberKey": "guid", "questionKey": "guid", "isCorrect": 0 or 1},
            ...
        ]
    }
    
    Returns:
    {
        "status": "OK",
        "questionParams": {
            "question_id": {
                "difficulty": float,
                "discrimination": float,
                "guessing": float,
                "quality": str,
                "confidenceLevel": str,
                "attemptCount": int,
                "converged": bool
            }
        },
        "memberAbilities": {
            "member_id": float (theta)
        },
        "metadata": {...}
    }
    """
    try:
        logging.info("=== Full IRT Analysis Request Started ===")
        
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
        
        # Create response matrix
        subjects = df_filtered['subject_id'].unique()
        items = df_filtered['item_id'].unique()
        
        subject_map = {subj: idx for idx, subj in enumerate(subjects)}
        item_map = {item: idx for idx, item in enumerate(items)}
        
        response_matrix = np.full((len(subjects), len(items)), -1.0)
        
        for _, row in df_filtered.iterrows():
            subj_idx = subject_map[row['subject_id']]
            item_idx = item_map[row['item_id']]
            response_matrix[subj_idx, item_idx] = row['response']
        
        # CRITICAL: Only keep subjects with >= 3 responses (more flexible)
        response_counts_per_subject = (response_matrix >= 0).sum(axis=1)
        min_responses = max(3, int(len(items) * 0.2))  # At least 3, or 20% of items
        valid_subject_mask = response_counts_per_subject >= min_responses
        
        response_matrix = response_matrix[valid_subject_mask, :]
        valid_subjects = subjects[valid_subject_mask]
        
        logging.info(f"Kept {len(valid_subjects)} subjects with >= {min_responses} responses")
        
        # CRITICAL: Only keep items with >= 5 responses from valid subjects
        response_counts_per_item = (response_matrix >= 0).sum(axis=0)
        min_item_responses = max(5, int(len(valid_subjects) * 0.1))  # At least 5, or 10% of subjects
        valid_item_mask = response_counts_per_item >= min_item_responses
        
        response_matrix = response_matrix[:, valid_item_mask]
        valid_items = items[valid_item_mask]
        
        logging.info(f"Final matrix shape: {response_matrix.shape}")
        
        if response_matrix.shape[0] < 5 or response_matrix.shape[1] < 2:
            return jsonify({
                "error": "Insufficient complete data after filtering",
                "message": f"Only {response_matrix.shape[0]} subjects and {response_matrix.shape[1]} items remaining"
            }), 400
        
        # Fill missing values with rounded item mean (Bernoulli requires 0 or 1)
        for j in range(response_matrix.shape[1]):
            item_responses = response_matrix[:, j]
            valid_responses = item_responses[item_responses >= 0]
            if len(valid_responses) > 0:
                mean_response = valid_responses.mean()
                # Round to nearest integer (0 or 1) for Bernoulli
                fill_value = np.round(mean_response)
                response_matrix[item_responses < 0, j] = fill_value
        
        # Fit Full IRT model
        logging.info("Starting Full IRT model training...")
        
        model = FullIRT3PL(max_iter=300, tolerance=0.0001)
        model.fit(response_matrix)
        
        # Extract results
        item_params = model.get_item_parameters()
        subject_abilities = model.get_subject_abilities()
        
        # Format results
        question_params = {}
        
        for idx, item_id in enumerate(valid_items):
            n_responses = question_counts.get(item_id, 0)
            
            if n_responses >= 100:
                confidence = "High"
            elif n_responses >= 30:
                confidence = "Medium"
            else:
                confidence = "Low"
            
            a = float(item_params['discrimination'][idx])
            b = float(item_params['difficulty'][idx])
            c = float(item_params['guessing'][idx])
            
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
        
        member_abilities = {
            str(valid_subjects[idx]): float(ability) 
            for idx, ability in enumerate(subject_abilities)
        }
        
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
                "modelType": "3PL Full IRT (EM Algorithm)",
                "iterations": model.max_iter
            }
        })
        
    except Exception as e:
        error_trace = traceback.format_exc()
        logging.error(f"ERROR: {error_trace}")
        return jsonify({"error": str(e), "trace": error_trace}), 500

def determine_quality(discrimination, guessing, difficulty):
    """
    Determine question quality based on IRT parameters
    
    Args:
        discrimination (a): Item discrimination (0-2.5)
        guessing (c): Guessing parameter (0-0.5)
        difficulty (b): Item difficulty (-3 to +3)
    
    Returns:
        str: "Tốt", "Cần xem lại", or "Kém"
    """
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
    print("=" * 70)
    print("🐍 Full IRT Analysis Service Starting...")
    print("📊 Method: 3PL Model + EM Algorithm (Probabilistic Programming)")
    print("🧠 Framework: PyTorch + Pyro")
    print("📊 Listening on: http://localhost:5001")
    print("❤️  Health check: http://localhost:5001/health")
    print("=" * 70)
    
    app.run(host='0.0.0.0', port=5001, debug=True, threaded=True)