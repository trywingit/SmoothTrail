using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 自定义拖尾渲染器 - 使用Catmull-Rom算法实现平滑轨迹
/// </summary>
public class 自定义拖尾 : MonoBehaviour
{
    // 成员变量使用 m_ 前缀
    [Header("拖尾参数")]
    [SerializeField] private int m_最大轨迹点数 = 50;
    [SerializeField] private float m_点间距阈值 = 0.1f;
    [SerializeField] private int m_插值精度 = 10;
    [SerializeField] private Color m_拖尾颜色 = Color.white;
    [SerializeField] private float m_拖尾宽度 = 0.1f;
    [SerializeField] private float m_最大存在时间 = 2.0f;
    [SerializeField] private bool m_是否暂停计时 = false;
    [SerializeField] private float m_张力参数 = 0.5f;

    // 组件引用
    public LineRenderer m_拖尾渲染器;
    private List<拖尾点数据> m_轨迹点列表 = new List<拖尾点数据>();

    // 暂停相关变量
    private float m_暂停开始时间 = 0f;
    private bool m_内部暂停状态 = false;

    /// <summary>
    /// 拖尾点数据结构，包含位置和时间信息
    /// </summary>
    private class 拖尾点数据
    {
        public Vector3 位置;
        public float 生成时间;
        public float 已存活时间;

        public 拖尾点数据(Vector3 位置, float 生成时间)
        {
            this.位置 = 位置;
            this.生成时间 = 生成时间;
            this.已存活时间 = 0f;
        }
    }

    void Start()
    {
        // 初始化拖尾渲染器
        初始化拖尾渲染器();
    }

    void Update()
    {
        // 更新暂停状态
        更新暂停状态();

        // 更新轨迹点
        更新轨迹点();

        // 生成平滑轨迹
        if (m_轨迹点列表.Count >= 2)
        {
            生成平滑轨迹();
        }
        else if (m_轨迹点列表.Count == 1)
        {
            // 只有一个点时直接显示
            m_拖尾渲染器.positionCount = 1;
            m_拖尾渲染器.SetPosition(0, m_轨迹点列表[0].位置);
        }
        else
        {
            m_拖尾渲染器.positionCount = 0;
        }
    }

    /// <summary>
    /// 初始化LineRenderer组件
    /// </summary>
    private void 初始化拖尾渲染器()
    {


        m_拖尾渲染器.useWorldSpace = true;
        m_拖尾渲染器.positionCount = 0;

    }

    /// <summary>
    /// 更新暂停状态
    /// </summary>
    private void 更新暂停状态()
    {
        if (m_是否暂停计时 && !m_内部暂停状态)
        {
            // 刚刚开启暂停
            m_内部暂停状态 = true;
            m_暂停开始时间 = Time.time;
        }
        else if (!m_是否暂停计时 && m_内部暂停状态)
        {
            // 刚刚关闭暂停，需要调整所有点的时间
            m_内部暂停状态 = false;
            float tmp_暂停持续时间 = Time.time - m_暂停开始时间;

            // 调整所有点的生成时间，相当于暂停期间时间没有流逝
            foreach (var tmp_点 in m_轨迹点列表)
            {
                tmp_点.生成时间 += tmp_暂停持续时间;
            }
        }
    }

    /// <summary>
    /// 更新轨迹点列表
    /// </summary>
    private void 更新轨迹点()
    {
        Vector3 tmp_当前位置 = transform.position;
        float tmp_当前时间 = Time.time;

        // 如果列表为空或距离足够远，添加新点
        if (m_轨迹点列表.Count == 0 ||
            Vector3.Distance(tmp_当前位置, m_轨迹点列表[m_轨迹点列表.Count - 1].位置) > m_点间距阈值)
        {
            m_轨迹点列表.Add(new 拖尾点数据(tmp_当前位置, tmp_当前时间));

            // 限制列表长度
            while (m_轨迹点列表.Count > m_最大轨迹点数)
            {
                m_轨迹点列表.RemoveAt(0);
            }
        }

        // 更新所有点的存活时间并移除过期点
        更新点存活时间并移除过期点();
    }

    /// <summary>
    /// 更新点存活时间并移除过期点
    /// </summary>
    private void 更新点存活时间并移除过期点()
    {
        // 更新存活时间
        for (int i = m_轨迹点列表.Count - 1; i >= 0; i--)
        {
            var tmp_点 = m_轨迹点列表[i];

            if (!m_内部暂停状态)
            {
                // 正常状态下，更新存活时间
                tmp_点.已存活时间 = Time.time - tmp_点.生成时间;
            }
            // 暂停状态下，存活时间保持不变

            // 移除存活时间过长的点（从列表开头开始移除）
            if (tmp_点.已存活时间 > m_最大存在时间)
            {
                m_轨迹点列表.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 使用Catmull-Rom算法生成平滑轨迹
    /// </summary>
    private void 生成平滑轨迹()
    {
        // 准备控制点（为Catmull-Rom算法添加虚拟点）
        Vector3[] tmp_控制点数组 = 准备控制点();

        // 生成平滑路径点
        List<Vector3> tmp_平滑路径点 = new List<Vector3>();

        for (int i = 0; i < tmp_控制点数组.Length - 3; i++)
        {
            Vector3 tmp_点0 = tmp_控制点数组[i];
            Vector3 tmp_点1 = tmp_控制点数组[i + 1];
            Vector3 tmp_点2 = tmp_控制点数组[i + 2];
            Vector3 tmp_点3 = tmp_控制点数组[i + 3];

            // 在每个段内生成插值点
            for (int j = 0; j <= m_插值精度; j++)
            {
                float tmp_插值比例 = j / (float)m_插值精度;
                Vector3 tmp_插值点 = 计算CatmullRom点(tmp_点0, tmp_点1, tmp_点2, tmp_点3, tmp_插值比例);
                tmp_平滑路径点.Add(tmp_插值点);
            }
        }

        tmp_平滑路径点.Reverse();
        // 更新LineRenderer
        m_拖尾渲染器.positionCount = tmp_平滑路径点.Count;
        m_拖尾渲染器.SetPositions(tmp_平滑路径点.ToArray());

    }

    /// <summary>
    /// 为Catmull-Rom插值准备控制点（处理边界条件）
    /// </summary>
    private Vector3[] 准备控制点()
    {
        if (m_轨迹点列表.Count < 2)
            return new Vector3[0];

        List<Vector3> tmp_控制点列表 = new List<Vector3>();

        // 添加起始虚拟点
        Vector3 tmp_起始虚拟点 = m_轨迹点列表[0].位置 +
            (m_轨迹点列表[0].位置 - m_轨迹点列表[1].位置);
        tmp_控制点列表.Add(tmp_起始虚拟点);

        // 添加原始点
        foreach (var tmp_点 in m_轨迹点列表)
        {
            tmp_控制点列表.Add(tmp_点.位置);
        }

        // 添加结束虚拟点
        Vector3 tmp_结束虚拟点 = m_轨迹点列表[m_轨迹点列表.Count - 1].位置 +
            (m_轨迹点列表[m_轨迹点列表.Count - 1].位置 - m_轨迹点列表[m_轨迹点列表.Count - 2].位置);
        tmp_控制点列表.Add(tmp_结束虚拟点);

        return tmp_控制点列表.ToArray();
    }



    /// <summary>
    /// Catmull-Rom插值核心算法
    /// </summary>
    private Vector3 计算CatmullRom点(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        // 使用 m_张力参数 替代原公式中的固定系数 0.5f

        float s = (1.0f - m_张力参数) * 0.5f; // 根据常见实现进行转换

        float t2 = t * t;
        float t3 = t2 * t;

        // 应用了张力参数的标准Catmull-Rom公式
        return (2 * p1) *s +
               ((-p0 + p2) * s) * t +
               ((2 * p0 - 5 * p1 + 4 * p2 - p3) * s) * t2 +
               ((-p0 + 3 * p1 - 3 * p2 + p3) * s) * t3;
    }



    /// <summary>
    /// 清空拖尾轨迹
    /// </summary>
    public void 清空拖尾()
    {
        m_轨迹点列表.Clear();
        if (m_拖尾渲染器 != null)
        {
            m_拖尾渲染器.positionCount = 0;
        }
    }

    /// <summary>
    /// 设置拖尾颜色
    /// </summary>
    public void 设置颜色(Color 新颜色)
    {
        m_拖尾颜色 = 新颜色;
        if (m_拖尾渲染器 != null)
        {
            m_拖尾渲染器.startColor = m_拖尾颜色;
            m_拖尾渲染器.endColor = m_拖尾颜色;
        }
    }

    /// <summary>
    /// 设置拖尾宽度
    /// </summary>
    public void 设置宽度(float 新宽度)
    {
        m_拖尾宽度 = 新宽度;
        if (m_拖尾渲染器 != null)
        {
            m_拖尾渲染器.startWidth = m_拖尾宽度;
            m_拖尾渲染器.endWidth = m_拖尾宽度;
        }
    }

    /// <summary>
    /// 设置是否暂停计时
    /// </summary>
    public void 设置暂停(bool 是否暂停)
    {
        m_是否暂停计时 = 是否暂停;
    }

    /// <summary>
    /// 强制恢复拖尾计时（无论暂停状态）
    /// </summary>
    public void 强制恢复计时()
    {
        m_是否暂停计时 = false;
        m_内部暂停状态 = false;

        // 重新计算所有点的存活时间
        float tmp_当前时间 = Time.time;
        foreach (var tmp_点 in m_轨迹点列表)
        {
            tmp_点.已存活时间 = tmp_当前时间 - tmp_点.生成时间;
        }
    }
}