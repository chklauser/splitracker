import {PointsVec} from "./PointsBlock";

export const enum ItemTypes {
  PointsPreview = 'PointsPreview'
}

export interface PointsPreviewData {
  points: PointsVec;
  channelingIndex?: number;
}
