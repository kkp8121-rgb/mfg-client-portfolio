namespace MkLike.Core.Save
{
    /// <summary>
    /// 세이브/로드 저장소 추상화 인터페이스.
    /// 로컬 파일, 서버 등 다양한 저장소 구현을 교체할 수 있도록 한다.
    /// </summary>
    public interface ISaveProvider
    {
        /// <summary>
        /// 데이터를 저장한다.
        /// </summary>
        /// <param name="data">저장할 세이브 데이터</param>
        void Save(SaveData data);

        /// <summary>
        /// 저장된 데이터를 불러온다.
        /// </summary>
        /// <returns>불러온 세이브 데이터. 저장된 데이터가 없으면 null을 반환할 수 있다.</returns>
        SaveData Load();

        /// <summary>
        /// 저장된 데이터가 존재하는지 확인한다.
        /// </summary>
        /// <returns>저장 데이터 존재 여부</returns>
        bool HasSave();

        /// <summary>
        /// 저장된 데이터를 삭제한다.
        /// </summary>
        void Delete();
    }
}
