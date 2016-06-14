using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 此算法思想来源于“http://www.cnblogs.com/sumtec/archive/2008/02/01/1061742.html”,经测试，检测"dddss屄defg东正教"这个字符串并替换掉敏感词（屄和东正教）平均花费1.7ms
/// </summary>
public class IllegalWordDetection
{
    /// <summary>
    /// 存了所有的长度大于1的敏感词汇
    /// </summary>
    static HashSet<string> wordsSet = new HashSet<string>();
    /// <summary>
    /// 存了某一个词在所有敏感词中的位置，（超出8个的截断为第8个位置）
    /// </summary>
    static byte[] fastCheck = new byte[char.MaxValue];
    /// <summary>
    /// 存了所有敏感词的长度信息，“Key”值为所有敏感词的第一个词，敏感词的长度会截断为8
    /// </summary>
    static byte[] fastLength = new byte[char.MaxValue];
    /// <summary>
    /// 保有所有敏感词汇的第一个词的记录，可用来判断是否一个词是一个或者多个敏感词汇的“第一个词”，且可判断以某一个词作为第一个词的一系列的敏感词的最大的长度
    /// </summary>
    static byte[] startCache = new byte[char.MaxValue];
    /// <summary>
    /// 保有所有敏感词汇的最后一个词的记录，仅用来判断是否一个词是一个或者多个敏感词汇的“最后一个词”
    /// </summary>
    static BitArray endCache = new BitArray(char.MaxValue);

    public static void Init(string[] badwords)
    {
        if (badwords == null || badwords.Length == 0)
            return;

        int wordLength = 0;
        for (int stringIndex = 0, len = badwords.Length; stringIndex < len; ++stringIndex)
        {
            ///求得单个的敏感词汇的长度
            wordLength = badwords[stringIndex].Length;
            if (wordLength <= 0)
                continue;

            for (int i = 0; i < wordLength; i++)
            {
                ///准确记录8位以内的敏感词汇的某个词在词汇中的“位置”
                if (i < 7)
                    fastCheck[badwords[stringIndex][i]] |= (byte)(1 << i);
                else///8位以外的敏感词汇的词直接限定在第8位
                    fastCheck[badwords[stringIndex][i]] |= 0x80;///0x80在内存中即为1000 0000，因为一个byte顶多标示8位，故超出8位的都位或上0x80，截断成第8位
            }

            ///缓存敏感词汇的长度
            int cacheWordslength = Math.Min(8, wordLength);
            ///记录敏感词汇的“大致长度（超出8个字的敏感词汇会被截取成8的长度）”，“key”值为敏感词汇的第一个词
            fastLength[badwords[stringIndex][0]] |= (byte)(1 << (cacheWordslength - 1));
            ///缓存出当前以badwords[stringIndex][0]词开头的一系列的敏感词汇的最长的长度
            if (startCache[badwords[stringIndex][0]] < cacheWordslength)
                startCache[badwords[stringIndex][0]] = (byte)(cacheWordslength);

            ///存好敏感词汇的最后一个词汇的“出现情况”
            endCache[badwords[stringIndex][wordLength - 1]] = true;
            ///将长度大于1的敏感词汇都压入到字典中
            if (!wordsSet.Contains(badwords[stringIndex]))
                wordsSet.Add(badwords[stringIndex]);
        }
    }

    /// <summary>
    /// 过滤字符串,默认遇到敏感词汇就以'*'代替
    /// </summary>
    /// <param name="text"></param>
    /// <param name="mask"></param>
    /// <returns></returns>
    unsafe public static string Filter(string text, string mask = "*")
    {
        var dic = DetectIllegalWords(text);
        ///如果没有敏感词汇，则直接返回出去
        if (dic.Count == 0)
            return text;

        fixed (char* newText = text, cMask = mask)
        {
            var itor = newText;
            ///开始替换敏感词汇
            foreach (var item in dic)
            {
                ///偏移到敏感词出现的位置
                itor = newText + item.Key;
                for (int index = 0; index < item.Value; index++)
                {
                    ///屏蔽掉敏感词汇
                    *itor++ = *cMask;
                }
            }
        }

        return text;
    }

    /// <summary>
    /// 判断text是否有敏感词汇,如果有返回敏感的词汇的位置,利用指针操作来加快运算速度,暂时独立出一个函数出来，不考虑代码复用的情况
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    unsafe public static bool IllegalWordsExistJudgement(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        int textLength = text.Length;
        fixed (char* ptext = text)
        {
            ///缓存字符串的初始位置
            char* itor = (fastCheck[*ptext] & 0x01) == 0 ? ptext + 1 : ptext;
            ///缓存字符串的末尾位置
            char* end = ptext + textLength;

            while (itor < end)
            {
                ///如果text的第一个词不是敏感词汇或者当前遍历到了text第一个词的后面的词，则循环检测到text词汇的倒数第二个词，看看这一段子字符串中有没有敏感词汇
                if ((fastCheck[*itor] & 0x01) == 0)
                {
                    while (itor < end - 1 && (fastCheck[*(++itor)] & 0x01) == 0) ;
                }

                ///如果有只有一个词的敏感词，且当前的字符串的“非第一个词”满足这个敏感词，则先加入已检测到的敏感词列表
                if (startCache[*itor] != 0 && (fastLength[*itor] & 0x01) > 0)
                {
                    return true;
                }

                ///此时已经检测到一个敏感词的“首词”了,记录下第一个检测到的敏感词的位置
                ///从当前的位置检测到字符串末尾
                int remainLength = (int)(end - itor - 1);
                for (int i = 1; i <= remainLength; ++i)
                {
                    char* subItor = itor + i;
                    ///如果检测到当前的词在所有敏感词中的位置信息中没有处在第i位的，则马上跳出遍历
                    if ((fastCheck[*subItor] >> Math.Min(i, 7)) == 0)
                    {
                        break;
                    }

                    ///如果有检测到敏感词的最后一个词，并且此时的“检测到的敏感词汇”的长度也符合要求，则才进一步查看检测到的敏感词汇是否是真的敏感
                    if ((fastLength[*itor] >> Math.Min(i - 1, 7)) > 0 && endCache[*subItor])
                    {
                        /// 如果要求的精度不高，此处可以直接返回true，不必要再hash常数次确保是指定敏感词
                        ///////////////////////////////////////////////////////////////////////////////////
                        int curIndex = (int)(itor - ptext);
                        ///暂时此处不考虑敏感词被隔开的情况
                        string sub = new string(itor, 0, i + 1);//text.Substring(curIndex, i + 1);
                        ///如果此子字符串在敏感词字典中存在，则返回
                        if (wordsSet.Contains(sub))
                        {
                            return true;
                        }
                        ///////////////////////////////////////////////////////////////////////////////////
                    }
                    else if (i > startCache[*itor] && startCache[*itor] < 0x80)///如果超过了以该词为首的一系列的敏感词汇的最大的长度，则不继续判断(前提是该词对应的所有敏感词汇没有超过8个词的)
                    {
                        break;
                    }
                }
                ++itor;
            }
        }

        return false;
    }

    /// <summary>
    /// 判断text是否有敏感词汇,如果有返回敏感的词汇的位置,利用指针操作来加快运算速度
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    unsafe public static Dictionary<int, int> DetectIllegalWords(string text)
    {
        var findResult = new Dictionary<int, int>();
        if (string.IsNullOrEmpty(text))
            return findResult;

        int textLength = text.Length;
        fixed (char* ptext = text)
        {
            ///缓存字符串的初始位置
            char* itor = (fastCheck[*ptext] & 0x01) == 0 ? ptext + 1 : ptext;
            ///缓存字符串的末尾位置
            char* end = ptext + textLength;

            while (itor < end)
            {
                ///如果text的第一个词不是敏感词汇或者当前遍历到了text第一个词的后面的词，则循环检测到text词汇的倒数第二个词，看看这一段子字符串中有没有敏感词汇
                if ((fastCheck[*itor] & 0x01) == 0)
                {
                    while (itor < end - 1 && (fastCheck[*(++itor)] & 0x01) == 0) ;
                }

                ///如果有只有一个词的敏感词，且当前的字符串的“非第一个词”满足这个敏感词，则先加入已检测到的敏感词列表
                if (startCache[*itor] != 0 && (fastLength[*itor] & 0x01) > 0)
                {
                    ///返回敏感词在text中的位置，以及敏感词的长度，供过滤功能用
                    findResult.Add((int)(itor - ptext), 1);
                }

                ///此时已经检测到一个敏感词的“首词”了,记录下第一个检测到的敏感词的位置
                ///从当前的位置检测到字符串末尾
                int remainLength = (int)(end - itor - 1);
                for (int i = 1; i <= remainLength; ++i)
                {
                    char* subItor = itor + i;
                    ///如果检测到当前的词在所有敏感词中的位置信息中没有处在第i位的，则马上跳出遍历
                    if ((fastCheck[*subItor] >> Math.Min(i, 7)) == 0)
                    {
                        break;
                    }

                    ///如果有检测到敏感词的最后一个词，并且此时的“检测到的敏感词汇”的长度也符合要求，则才进一步查看检测到的敏感词汇是否是真的敏感
                    if ((fastLength[*itor] >> Math.Min(i - 1, 7)) > 0 && endCache[*subItor])
                    {
                        int curIndex = (int)(itor - ptext);
                        ///暂时此处不考虑敏感词被隔开的情况
                        string sub = new string(itor, 0, i + 1);//text.Substring(curIndex, i + 1);
                        ///如果此子字符串在敏感词字典中存在，则记录。做此判断是避免敏感词中夹杂了其他敏感词的单词，而上面的算法无法剔除，故先用hash数组来剔除
                        ///上述算法是用于减少大部分的比较消耗
                        if (wordsSet.Contains(sub))
                        {
                            findResult[curIndex] = i + 1;
                        }
                    }
                    else if (i > startCache[*itor] && startCache[*itor] < 0x80)///如果超过了以该词为首的一系列的敏感词汇的最大的长度，则不继续判断(前提是该词对应的所有敏感词汇没有超过8个词的)
                    {
                        break;
                    }
                }
                ++itor;
            }
        }

        return findResult;
    }
}