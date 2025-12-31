namespace DataWarehouse.AI
{
    /// <summary>
    /// The brains
    /// </summary>
    public static class VectorMath
    {
        /// <summary>
        /// Cosine similarity
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        /// <returns></returns>
        public static float CosineSimilarity(float[] v1, float[] v2)
        {
            if (v1.Length != v2.Length) return 0f;
            float dot = 0f, mag1 = 0f, mag2 = 0f;
            for (int i = 0; i < v1.Length; i++)
            {
                dot += v1[i] * v2[i];
                mag1 += v1[i] * v1[i];
                mag2 += v2[i] * v2[i];
            }
            return (mag1 > 0 && mag2 > 0) ? dot / (MathF.Sqrt(mag1) * MathF.Sqrt(mag2)) : 0f;
        }

        /// <summary>
        /// 1 - CosineSimilarity (0 = identical, 2 = opposite)
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        /// <returns></returns>
        public static float Distance(float[] v1, float[] v2)
        {
            float dot = 0, m1 = 0, m2 = 0;
            for (int i = 0; i < v1.Length; i++) { dot += v1[i] * v2[i]; m1 += v1[i] * v1[i]; m2 += v2[i] * v2[i]; }
            float similarity = (m1 > 0 && m2 > 0) ? dot / (MathF.Sqrt(m1) * MathF.Sqrt(m2)) : 0;
            return 1.0f - similarity;
        }
    }
}