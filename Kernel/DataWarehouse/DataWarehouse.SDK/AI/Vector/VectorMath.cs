using DataWarehouse.SDK.AI.Math;

namespace DataWarehouse.SDK.AI.Vector
{
    /// <summary>
    /// Utility class for vector mathematics operations.
    /// Provides functions for similarity calculation, normalization, and clustering.
    ///
    /// Used by:
    /// - IVectorStore implementations for similarity search
    /// - AI Runtime for embedding comparison
    /// - Clustering algorithms for capability grouping
    /// </summary>
    public static class VectorMath
    {
        /// <summary>
        /// Computes cosine similarity between two vectors.
        /// Cosine similarity measures the cosine of the angle between two vectors.
        ///
        /// Formula: cos(θ) = (A · B) / (||A|| × ||B||)
        ///
        /// Returns:
        /// - 1.0: Vectors point in the same direction (identical semantic meaning)
        /// - 0.0: Vectors are orthogonal (unrelated)
        /// - -1.0: Vectors point in opposite directions (opposing meaning)
        ///
        /// For embeddings, values typically range 0.6-1.0.
        /// </summary>
        /// <param name="a">First vector.</param>
        /// <param name="b">Second vector.</param>
        /// <returns>Cosine similarity (-1.0 to 1.0).</returns>
        /// <exception cref="ArgumentException">If vectors have different dimensions.</exception>
        public static float CosineSimilarity(float[] a, float[] b)
        {
            if (a.Length != b.Length)
                throw new ArgumentException($"Vector dimensions must match: {a.Length} vs {b.Length}");

            if (a.Length == 0)
                throw new ArgumentException("Vectors cannot be empty");

            // Compute dot product: A · B
            float dotProduct = 0;
            for (int i = 0; i < a.Length; i++)
            {
                dotProduct += a[i] * b[i];
            }

            // Compute magnitudes: ||A|| and ||B||
            float magnitudeA = 0;
            float magnitudeB = 0;
            for (int i = 0; i < a.Length; i++)
            {
                magnitudeA += a[i] * a[i];
                magnitudeB += b[i] * b[i];
            }
            magnitudeA = (float)MathUtils.Sqrt(magnitudeA);
            magnitudeB = (float)MathUtils.Sqrt(magnitudeB);

            // Handle zero vectors
            if (magnitudeA == 0 || magnitudeB == 0)
                return 0;

            // Compute cosine similarity
            return dotProduct / (magnitudeA * magnitudeB);
        }

        /// <summary>
        /// Normalizes a vector to unit length (magnitude = 1).
        /// Normalized vectors make cosine similarity computation faster.
        ///
        /// Formula: A_normalized = A / ||A||
        ///
        /// Benefits:
        /// - For normalized vectors, cosine similarity = dot product
        /// - Reduces computation in similarity search
        /// - Standardizes vector magnitudes
        /// </summary>
        /// <param name="vector">Vector to normalize.</param>
        /// <returns>Normalized vector with magnitude 1.</returns>
        public static float[] Normalize(float[] vector)
        {
            if (vector.Length == 0)
                throw new ArgumentException("Vector cannot be empty");

            // Compute magnitude
            float magnitude = 0;
            for (int i = 0; i < vector.Length; i++)
            {
                magnitude += vector[i] * vector[i];
            }
            magnitude = (float)MathUtils.Sqrt(magnitude);

            // Handle zero vector
            if (magnitude == 0)
                return new float[vector.Length]; // Return zero vector

            // Normalize
            float[] normalized = new float[vector.Length];
            for (int i = 0; i < vector.Length; i++)
            {
                normalized[i] = vector[i] / magnitude;
            }

            return normalized;
        }

        /// <summary>
        /// Computes the dot product of two vectors.
        /// For normalized vectors, dot product equals cosine similarity.
        ///
        /// Formula: A · B = Σ(a_i × b_i)
        /// </summary>
        /// <param name="a">First vector.</param>
        /// <param name="b">Second vector.</param>
        /// <returns>Dot product.</returns>
        /// <exception cref="ArgumentException">If vectors have different dimensions.</exception>
        public static float DotProduct(float[] a, float[] b)
        {
            if (a.Length != b.Length)
                throw new ArgumentException($"Vector dimensions must match: {a.Length} vs {b.Length}");

            float result = 0;
            for (int i = 0; i < a.Length; i++)
            {
                result += a[i] * b[i];
            }
            return result;
        }

        /// <summary>
        /// Computes the Euclidean distance between two vectors.
        /// Euclidean distance measures straight-line distance in vector space.
        ///
        /// Formula: ||A - B|| = √(Σ(a_i - b_i)²)
        ///
        /// Use cases:
        /// - Clustering (K-means)
        /// - Anomaly detection
        /// - Alternative to cosine similarity
        ///
        /// Note: Cosine similarity is often preferred for embeddings
        /// because it's magnitude-invariant.
        /// </summary>
        /// <param name="a">First vector.</param>
        /// <param name="b">Second vector.</param>
        /// <returns>Euclidean distance (always >= 0).</returns>
        /// <exception cref="ArgumentException">If vectors have different dimensions.</exception>
        public static float EuclideanDistance(float[] a, float[] b)
        {
            if (a.Length != b.Length)
                throw new ArgumentException($"Vector dimensions must match: {a.Length} vs {b.Length}");

            float sumSquares = 0;
            for (int i = 0; i < a.Length; i++)
            {
                float diff = a[i] - b[i];
                sumSquares += diff * diff;
            }
            return (float)MathUtils.Sqrt(sumSquares);
        }

        /// <summary>
        /// Computes the magnitude (L2 norm) of a vector.
        ///
        /// Formula: ||A|| = √(Σ(a_i²))
        /// </summary>
        /// <param name="vector">Vector to measure.</param>
        /// <returns>Magnitude (always >= 0).</returns>
        public static float Magnitude(float[] vector)
        {
            float sumSquares = 0;
            for (int i = 0; i < vector.Length; i++)
            {
                sumSquares += vector[i] * vector[i];
            }
            return (float)MathUtils.Sqrt(sumSquares);
        }

        /// <summary>
        /// Performs K-means clustering on a set of vectors.
        /// Groups similar vectors into K clusters.
        ///
        /// Algorithm:
        /// 1. Initialize K random centroids
        /// 2. Assign each vector to nearest centroid
        /// 3. Recompute centroids as mean of assigned vectors
        /// 4. Repeat until convergence
        ///
        /// Use cases:
        /// - Group similar capabilities
        /// - Identify capability categories
        /// - Detect redundant plugins
        ///
        /// Note: This is a simple implementation. For production,
        /// consider using specialized libraries (e.g., Accord.NET).
        /// </summary>
        /// <param name="vectors">Vectors to cluster.</param>
        /// <param name="k">Number of clusters.</param>
        /// <param name="maxIterations">Maximum iterations before stopping (default 100).</param>
        /// <returns>Cluster assignments (array of cluster indices 0 to k-1).</returns>
        public static int[] KMeansClustering(List<float[]> vectors, int k, int maxIterations = 100)
        {
            if (vectors.Count == 0)
                throw new ArgumentException("Vector list cannot be empty");
            if (k <= 0 || k > vectors.Count)
                throw new ArgumentException($"K must be between 1 and {vectors.Count}");

            int dimensions = vectors[0].Length;
            int n = vectors.Count;

            // Initialize centroids randomly
            var random = new Random(42); // Fixed seed for reproducibility
            var centroids = new float[k][];
            var usedIndices = new HashSet<int>();
            for (int i = 0; i < k; i++)
            {
                int index;
                do
                {
                    index = random.Next(n);
                } while (usedIndices.Contains(index));
                usedIndices.Add(index);
                centroids[i] = (float[])vectors[index].Clone();
            }

            // Cluster assignments
            var assignments = new int[n];
            var previousAssignments = new int[n];

            // Iterate until convergence or max iterations
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                // Assign each vector to nearest centroid
                for (int i = 0; i < n; i++)
                {
                    float minDistance = float.MaxValue;
                    int closestCentroid = 0;

                    for (int j = 0; j < k; j++)
                    {
                        float distance = EuclideanDistance(vectors[i], centroids[j]);
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            closestCentroid = j;
                        }
                    }

                    assignments[i] = closestCentroid;
                }

                // Check for convergence
                bool converged = true;
                for (int i = 0; i < n; i++)
                {
                    if (assignments[i] != previousAssignments[i])
                    {
                        converged = false;
                        break;
                    }
                }
                if (converged)
                    break;

                Array.Copy(assignments, previousAssignments, n);

                // Recompute centroids
                var clusterSums = new float[k][];
                var clusterCounts = new int[k];
                for (int i = 0; i < k; i++)
                {
                    clusterSums[i] = new float[dimensions];
                }

                for (int i = 0; i < n; i++)
                {
                    int cluster = assignments[i];
                    clusterCounts[cluster]++;
                    for (int d = 0; d < dimensions; d++)
                    {
                        clusterSums[cluster][d] += vectors[i][d];
                    }
                }

                for (int i = 0; i < k; i++)
                {
                    if (clusterCounts[i] > 0)
                    {
                        for (int d = 0; d < dimensions; d++)
                        {
                            centroids[i][d] = clusterSums[i][d] / clusterCounts[i];
                        }
                    }
                }
            }

            return assignments;
        }

        /// <summary>
        /// Computes pairwise cosine similarities for a list of vectors.
        /// Returns a similarity matrix where matrix[i][j] = similarity(vectors[i], vectors[j]).
        ///
        /// Use cases:
        /// - Find all similar capabilities
        /// - Detect redundant plugins
        /// - Visualize capability relationships
        /// </summary>
        /// <param name="vectors">List of vectors.</param>
        /// <returns>N×N similarity matrix.</returns>
        public static float[][] PairwiseSimilarities(List<float[]> vectors)
        {
            int n = vectors.Count;
            var matrix = new float[n][];

            for (int i = 0; i < n; i++)
            {
                matrix[i] = new float[n];
                for (int j = 0; j < n; j++)
                {
                    if (i == j)
                    {
                        matrix[i][j] = 1.0f; // Perfect similarity with self
                    }
                    else if (j > i)
                    {
                        // Compute similarity
                        float similarity = CosineSimilarity(vectors[i], vectors[j]);
                        matrix[i][j] = similarity;
                        matrix[j][i] = similarity; // Symmetric
                    }
                }
            }

            return matrix;
        }
    }
}
